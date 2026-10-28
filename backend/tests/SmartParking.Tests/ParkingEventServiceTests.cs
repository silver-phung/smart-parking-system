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
    public async Task IngestAsync_VehicleEntered_ShouldCreatePendingSession()
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
        Assert.Equal(ParkingSessionStatus.PendingSlot, result.SessionStatus);
        Assert.False(string.IsNullOrEmpty(result.SessionId));

        var active = await _sessionRepo.FindActiveByCarPlateAsync("51F-12345");
        Assert.NotNull(active);
        Assert.Equal(result.SessionId, active.SessionId);
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
    public async Task IngestAsync_ActiveCarCheckInAgain_ShouldReject()
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
        await _service.IngestAsync(first);

        // Act - Car tries to enter again without exiting
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
        Assert.False(result.Success);
        Assert.Equal(EventProcessingStatus.Rejected, result.ProcessingStatus);
    }

    [Fact]
    public async Task IngestAsync_FullLifecycle_Entered_Parked_Exited()
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
            Source = "ZoneStaff",
            TriggeredBy = "staff_01"
        };
        var parkRes = await _service.IngestAsync(parkReq);
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
            Source = "GateStaff",
            TriggeredBy = "gate_staff_02"
        };
        var exitRes = await _service.IngestAsync(exitReq);
        Assert.True(exitRes.Success);
        Assert.Equal(ParkingSessionStatus.Exited, exitRes.SessionStatus);

        // 4. Verify plate is now free to check in again
        var reenterReq = new IngestEventRequest
        {
            EventId = Guid.NewGuid().ToString(),
            ParkingLotId = 1,
            CarPlate = plate,
            EventType = ParkingEventType.VehicleEntered,
            Source = "CameraGate01"
        };
        var reenterRes = await _service.IngestAsync(reenterReq);
        Assert.True(reenterRes.Success);
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
            Source       = "ZoneStaff",
            TriggeredBy  = "staff_01"
        };

        // Act
        var result = await _service.IngestAsync(request);

        // Assert - should not reject, should flag as pending_verification
        Assert.False(result.Success);
        Assert.Equal(EventProcessingStatus.PendingVerification, result.ProcessingStatus);
    }

    [Fact]
    public async Task IngestAsync_VehicleExited_OnAlreadyExitedSession_ShouldReject()
    {
        var plate = "51F-77001";
        // Enter
        var enterRes = await _service.IngestAsync(new IngestEventRequest
        {
            EventId      = Guid.NewGuid().ToString(), ParkingLotId = 1, CarPlate = plate,
            EventType    = ParkingEventType.VehicleEntered, Source = "CameraGate01"
        });
        var sessionId = enterRes.SessionId!;

        // Exit
        await _service.IngestAsync(new IngestEventRequest
        {
            EventId   = Guid.NewGuid().ToString(), SessionId = sessionId, ParkingLotId = 1,
            CarPlate  = plate, EventType = ParkingEventType.VehicleExited, Source = "GateStaff"
        });

        // Exit again - should reject
        var result = await _service.IngestAsync(new IngestEventRequest
        {
            EventId   = Guid.NewGuid().ToString(), SessionId = sessionId, ParkingLotId = 1,
            CarPlate  = plate, EventType = ParkingEventType.VehicleExited, Source = "GateStaff"
        });

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
}
