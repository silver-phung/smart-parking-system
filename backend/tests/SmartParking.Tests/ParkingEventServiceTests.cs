using SmartParking.Application.Events;
using SmartParking.Application.Events.Dtos;
using SmartParking.Domain.Entities;
using SmartParking.Infrastructure.Repositories;
using SmartParking.Infrastructure.Services;
using Xunit;

namespace SmartParking.Tests;

public class ParkingEventServiceTests
{
    private readonly InMemoryParkingEventLogRepository _eventLogRepo = new();
    private readonly InMemoryParkingSessionRepository _sessionRepo = new();
    private readonly ParkingEventService _service;

    public ParkingEventServiceTests()
    {
        _service = new ParkingEventService(_eventLogRepo, _sessionRepo, new AlwaysCompatibleVehicleChecker());
    }

    [Fact]
    public async Task IngestAsync_VehicleEntered_ShouldCreateWaitingVerificationSession()
    {
        // Arrange
        var request = new IngestEventRequest
        {
            EventId = Guid.NewGuid().ToString(),
            ParkingLotId = 1,
            CarPlate = "51F-12345",
            EventType = ParkingEventType.VehicleEntered,
            Source = "CameraGate01",
            FuelType = "gasoline",
            CarColor = "white",
            CarModel = "Toyota Vios"
        };

        // Act
        var result = await _service.IngestAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(EventProcessingStatus.Accepted, result.ProcessingStatus);
        Assert.Equal(ParkingSessionStatus.WaitingVerification, result.SessionStatus);
        Assert.False(string.IsNullOrEmpty(result.SessionId));

        var active = await _sessionRepo.FindBySessionIdAsync(result.SessionId!);
        Assert.NotNull(active);
        Assert.Equal(result.SessionId, active.SessionId);
        Assert.Equal("51F-12345", active.RecognizedPlate);
        Assert.Empty(active.CarPlate);
    }

    [Fact]
    public async Task IngestAsync_DuplicateEventId_ShouldReturnDuplicateWithoutReprocessing()
    {
        // Arrange
        var eventId = Guid.NewGuid().ToString();
        var request = new IngestEventRequest
        {
            EventId = eventId,
            ParkingLotId = 1,
            CarPlate = "30A-99999",
            EventType = ParkingEventType.VehicleEntered,
            Source = "CameraGate01"
        };

        // Act - First call
        var firstResult = await _service.IngestAsync(request);
        Assert.True(firstResult.Success);
        Assert.False(firstResult.IsDuplicate);

        // Act - Second call with exact same EventId
        var secondResult = await _service.IngestAsync(request);

        // Assert
        Assert.True(secondResult.Success);
        Assert.True(secondResult.IsDuplicate);
        Assert.Equal(EventProcessingStatus.Duplicate, secondResult.ProcessingStatus);
    }

    [Fact]
    public async Task IngestAsync_SameOcrPlateAtGate_ShouldNotBeTreatedAsVerifiedDuplicate()
    {
        // Arrange
        var plate = "43B-55555";
        var first = new IngestEventRequest
        {
            EventId = Guid.NewGuid().ToString(),
            ParkingLotId = 1,
            CarPlate = plate,
            EventType = ParkingEventType.VehicleEntered,
            Source = "CameraGate01"
        };
        var firstResult = await _service.IngestAsync(first);

        // OCR có thể sai/trùng; chỉ biển số đã xác minh tại slot mới là khóa nghiệp vụ.
        var second = new IngestEventRequest
        {
            EventId = Guid.NewGuid().ToString(),
            ParkingLotId = 1,
            CarPlate = plate,
            EventType = ParkingEventType.VehicleEntered,
            Source = "CameraGate01"
        };
        var result = await _service.IngestAsync(second);

        // Assert
        Assert.True(result.Success);
        Assert.NotEqual(firstResult.SessionId, result.SessionId);
    }

    [Fact]
    public async Task IngestAsync_Entry_VerifyAtSlot_ExitSignal_DoesNotCloseSession()
    {
        var plate = "72E-77777";

        // 1. VehicleEntered
        var enterReq = new IngestEventRequest
        {
            EventId = Guid.NewGuid().ToString(),
            ParkingLotId = 1,
            CarPlate = plate,
            EventType = ParkingEventType.VehicleEntered,
            Source = "CameraGate01"
        };
        var enterRes = await _service.IngestAsync(enterReq);
        Assert.True(enterRes.Success);
        var sessionId = enterRes.SessionId!;

        // 2. SlotParked by Staff
        var parkReq = new IngestEventRequest
        {
            EventId = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            ParkingLotId = 1,
            SlotCode = "A1-012",
            CarPlate = plate,
            EventType = ParkingEventType.SlotParked,
            Source = "ZoneStaff"
        };
        var parkRes = await _service.IngestAsync(parkReq, "staff_01");
        Assert.True(parkRes.Success);
        Assert.Equal(ParkingSessionStatus.Parked, parkRes.SessionStatus);

        // 3. VehicleExited
        var exitReq = new IngestEventRequest
        {
            EventId = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            ParkingLotId = 1,
            CarPlate = plate,
            EventType = ParkingEventType.VehicleExited,
            Source = "GateStaff"
        };
        var exitRes = await _service.IngestAsync(exitReq, "gate_staff_02");
        Assert.True(exitRes.Success);
        Assert.Equal(ParkingSessionStatus.ExitProcessing, exitRes.SessionStatus);

        var session = await _sessionRepo.FindBySessionIdAsync(sessionId);
        Assert.NotNull(session);
        Assert.Null(session.ExitedAtUtc);
        Assert.Equal("A1-012", session.SlotCode);
    }

    [Fact]
    public async Task IngestAsync_SlotParked_WithoutAuthenticatedStaff_ShouldReject()
    {
        var entered = await _service.IngestAsync(new IngestEventRequest
        {
            EventId = Guid.NewGuid().ToString(),
            ParkingLotId = 1,
            CarPlate = "51F-11111",
            EventType = ParkingEventType.VehicleEntered,
            Source = "CameraGate01"
        });

        var result = await _service.IngestAsync(new IngestEventRequest
        {
            EventId = Guid.NewGuid().ToString(),
            SessionId = entered.SessionId,
            ParkingLotId = 1,
            SlotCode = "A1-012",
            CarPlate = "51F-11111",
            EventType = ParkingEventType.SlotParked,
            Source = "ZoneStaff"
        });

        Assert.False(result.Success);
        Assert.Equal(EventProcessingStatus.Rejected, result.ProcessingStatus);
    }

    [Fact]
    public async Task IngestAsync_InvalidEventId_ShouldRejectBeforeWriting()
    {
        var result = await _service.IngestAsync(new IngestEventRequest
        {
            EventId = "not-a-uuid",
            ParkingLotId = 1,
            EventType = ParkingEventType.VehicleEntered,
            Source = "CameraGate01"
        });

        Assert.False(result.Success);
        Assert.Equal(EventProcessingStatus.Rejected, result.ProcessingStatus);
    }

    // ─── DAY 4: New Hardening Tests ────────────────────────────────────────────

    [Fact]
    public async Task IngestAsync_SlotParked_WithoutActiveSession_ShouldReturnPendingVerification()
    {
        // Arrange - SlotParked arrives but no VehicleEntered was sent first
        var request = new IngestEventRequest
        {
            EventId      = Guid.NewGuid().ToString(),
            SessionId    = Guid.NewGuid().ToString(), // random, not in system
            ParkingLotId = 1,
            SlotCode     = "A1-012",
            CarPlate     = "51F-00001",
            EventType    = ParkingEventType.SlotParked,
            Source       = "ZoneStaff"
        };

        // Act
        var result = await _service.IngestAsync(request, "staff_01");

        // Assert - should not reject, should flag as pending_verification
        Assert.False(result.Success);
        Assert.Equal(EventProcessingStatus.PendingVerification, result.ProcessingStatus);
    }

    [Fact]
    public async Task IngestAsync_VehicleExited_OnExitProcessingSession_ShouldReject()
    {
        var plate = "51F-77001";
        // Enter
        var enterRes = await _service.IngestAsync(new IngestEventRequest
        {
            EventId      = Guid.NewGuid().ToString(), ParkingLotId = 1, CarPlate = plate,
            EventType    = ParkingEventType.VehicleEntered, Source = "CameraGate01"
        });
        var sessionId = enterRes.SessionId!;

        // Verify at slot first
        await _service.IngestAsync(new IngestEventRequest
        {
            EventId = Guid.NewGuid().ToString(), SessionId = sessionId, ParkingLotId = 1,
            SlotCode = "A1-021", CarPlate = plate, FuelType = "gasoline",
            EventType = ParkingEventType.SlotParked, Source = "ZoneStaff"
        }, "staff_01");

        // First exit signal
        await _service.IngestAsync(new IngestEventRequest
        {
            EventId   = Guid.NewGuid().ToString(), SessionId = sessionId, ParkingLotId = 1,
            CarPlate  = plate, EventType = ParkingEventType.VehicleExited, Source = "GateStaff"
        }, "gate_staff_01");

        // Exit again - should reject
        var result = await _service.IngestAsync(new IngestEventRequest
        {
            EventId   = Guid.NewGuid().ToString(), SessionId = sessionId, ParkingLotId = 1,
            CarPlate  = plate, EventType = ParkingEventType.VehicleExited, Source = "GateStaff"
        }, "gate_staff_01");

        Assert.False(result.Success);
        Assert.Equal(EventProcessingStatus.Rejected, result.ProcessingStatus);
    }

    [Fact]
    public async Task IngestAsync_SensorTriggered_WithMissingSlotCode_ShouldReject()
    {
        var request = new IngestEventRequest
        {
            EventId      = Guid.NewGuid().ToString(),
            ParkingLotId = 1,
            SlotCode     = null, // missing
            EventType    = ParkingEventType.SensorTriggered,
            Source       = "IoTSensor_B2-001"
        };

        var result = await _service.IngestAsync(request);

        Assert.False(result.Success);
        Assert.Equal(EventProcessingStatus.Rejected, result.ProcessingStatus);
    }

    [Fact]
    public async Task IngestAsync_VehicleExited_WithNoSession_ShouldReturnPendingVerification()
    {
        var request = new IngestEventRequest
        {
            EventId      = Guid.NewGuid().ToString(),
            SessionId    = Guid.NewGuid().ToString(), // unknown session
            ParkingLotId = 1,
            CarPlate     = "51F-99999",
            EventType    = ParkingEventType.VehicleExited,
            Source       = "GateStaff"
        };

        var result = await _service.IngestAsync(request);

        Assert.False(result.Success);
        Assert.Equal(EventProcessingStatus.PendingVerification, result.ProcessingStatus);
    }

    [Fact]
    public async Task IngestAsync_TwoSessionsCannotOccupySameSlot()
    {
        async Task<string> Enter(string plate)
        {
            var response = await _service.IngestAsync(new IngestEventRequest
            {
                EventId = Guid.NewGuid().ToString(), ParkingLotId = 7, CarPlate = plate,
                EventType = ParkingEventType.VehicleEntered, Source = "CameraGate01"
            });
            return response.SessionId!;
        }

        var firstSession = await Enter("51F-70001");
        var secondSession = await Enter("51F-70002");

        var first = await _service.IngestAsync(new IngestEventRequest
        {
            EventId = Guid.NewGuid().ToString(), SessionId = firstSession, ParkingLotId = 7,
            SlotCode = "A1-099", CarPlate = "51F-70001", FuelType = "gasoline",
            EventType = ParkingEventType.SlotParked, Source = "ZoneStaff"
        }, "staff_01");
        var second = await _service.IngestAsync(new IngestEventRequest
        {
            EventId = Guid.NewGuid().ToString(), SessionId = secondSession, ParkingLotId = 7,
            SlotCode = "A1-099", CarPlate = "51F-70002", FuelType = "gasoline",
            EventType = ParkingEventType.SlotParked, Source = "ZoneStaff"
        }, "staff_02");

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Contains("đang được một phiên khác chiếm", second.Message);
    }

    [Fact]
    public async Task IngestAsync_OlderExitEventGoesToPendingVerification()
    {
        var entered = await _service.IngestAsync(new IngestEventRequest
        {
            EventId = Guid.NewGuid().ToString(), ParkingLotId = 8, CarPlate = "51F-80001",
            EventTimestampUtc = DateTimeOffset.UtcNow,
            EventType = ParkingEventType.VehicleEntered, Source = "CameraGate01"
        });
        var parkedAt = DateTimeOffset.UtcNow.AddMinutes(1);
        await _service.IngestAsync(new IngestEventRequest
        {
            EventId = Guid.NewGuid().ToString(), SessionId = entered.SessionId, ParkingLotId = 8,
            SlotCode = "B2-088", CarPlate = "51F-80001", FuelType = "gasoline",
            EventTimestampUtc = parkedAt,
            EventType = ParkingEventType.SlotParked, Source = "ZoneStaff"
        }, "staff_01");

        var result = await _service.IngestAsync(new IngestEventRequest
        {
            EventId = Guid.NewGuid().ToString(), SessionId = entered.SessionId, ParkingLotId = 8,
            EventTimestampUtc = parkedAt.AddMinutes(-2),
            EventType = ParkingEventType.VehicleExited, Source = "GateCamera"
        });

        Assert.False(result.Success);
        Assert.Equal(EventProcessingStatus.PendingVerification, result.ProcessingStatus);
    }

    [Fact]
    public async Task IngestAsync_ConcurrentRetryAppliesOnlyOnce()
    {
        var request = new IngestEventRequest
        {
            EventId = Guid.NewGuid().ToString(), ParkingLotId = 9, CarPlate = "51F-90001",
            EventType = ParkingEventType.VehicleEntered, Source = "CameraGate01"
        };

        var results = await Task.WhenAll(
            _service.IngestAsync(request),
            _service.IngestAsync(request));

        Assert.Single(results.Where(r => !r.IsDuplicate));
        Assert.Single(results.Where(r => r.IsDuplicate));
    }
}
