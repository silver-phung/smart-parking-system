using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SmartParking.Application.Events.Dtos;
using SmartParking.Domain.Entities;
using Xunit;

namespace SmartParking.Tests;

/// <summary>
/// Integration smoke tests – khởi động toàn bộ WebApplication trong memory
/// và gọi HTTP endpoint thực sự qua WebApplicationFactory.
/// </summary>
public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    // ─── Existing infrastructure tests ───────────────────────────────────────

    [Fact]
    public async Task GetHealth_ReturnsOkStatusCode()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSystemInfo_ReturnsExpectedPayload()
    {
        var response = await _client.GetAsync("/api/system/info");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Smart Parking API", content);
    }

    [Fact]
    public async Task GetSystemHealth_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/api/system/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", content);
    }

    [Fact]
    public async Task GetSwaggerJson_ReturnsValidOpenApiSpecification()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Smart Parking API", content);
    }

    // ─── Sprint 3 Day 5: Event Ingestion API smoke tests ─────────────────────

    /// <summary>
    /// Xe vào cổng → tạo phiên pending_slot thành công.
    /// </summary>
    [Fact]
    public async Task PostEvent_VehicleEntered_Returns200WithAccepted()
    {
        // Arrange
        var request = new IngestEventRequest
        {
            EventId      = Guid.NewGuid().ToString(),
            ParkingLotId = 1,
            CarPlate     = "72F-IntTest01",
            EventType    = ParkingEventType.VehicleEntered,
            Source       = "IntegrationTest",
            FuelType     = "gasoline",
            CarColor     = "blue",
            CarModel     = "Honda City"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/events", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<IngestEventResponse>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(EventProcessingStatus.Accepted, result.ProcessingStatus);
        Assert.Equal(ParkingSessionStatus.PendingSlot, result.SessionStatus);
        Assert.False(string.IsNullOrEmpty(result.SessionId));
    }

    /// <summary>
    /// Gửi lại cùng EventId → hệ thống phát hiện duplicate, không xử lý lần 2.
    /// </summary>
    [Fact]
    public async Task PostEvent_DuplicateEventId_Returns200WithDuplicateFlag()
    {
        var eventId = Guid.NewGuid().ToString();
        var request = new IngestEventRequest
        {
            EventId      = eventId,
            ParkingLotId = 1,
            CarPlate     = "72F-IntTest02",
            EventType    = ParkingEventType.VehicleEntered,
            Source       = "IntegrationTest"
        };

        // First request
        await _client.PostAsJsonAsync("/api/events", request);

        // Retry with same EventId
        var response = await _client.PostAsJsonAsync("/api/events", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<IngestEventResponse>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.True(result.IsDuplicate);
        Assert.Equal(EventProcessingStatus.Duplicate, result.ProcessingStatus);
    }

    /// <summary>
    /// Full lifecycle: VehicleEntered → SlotParked → VehicleExited.
    /// </summary>
    [Fact]
    public async Task PostEvent_FullLifecycle_Returns200AtEveryStep()
    {
        var plate = "72F-IntTest03";

        // Step 1: Enter
        var enterRes = await _client.PostAsJsonAsync("/api/events", new IngestEventRequest
        {
            EventId = Guid.NewGuid().ToString(), ParkingLotId = 1, CarPlate = plate,
            EventType = ParkingEventType.VehicleEntered, Source = "IntegrationTest"
        });
        Assert.Equal(HttpStatusCode.OK, enterRes.StatusCode);
        var enterDto = await enterRes.Content.ReadFromJsonAsync<IngestEventResponse>();
        Assert.NotNull(enterDto);
        var sessionId = enterDto.SessionId!;

        // Step 2: Slot assigned by Staff
        var parkRes = await _client.PostAsJsonAsync("/api/events", new IngestEventRequest
        {
            EventId = Guid.NewGuid().ToString(), SessionId = sessionId,
            ParkingLotId = 1, SlotCode = "A1-012", CarPlate = plate,
            EventType = ParkingEventType.SlotParked,
            Source = "ZoneStaff", TriggeredBy = "zone_staff_01"
        });
        Assert.Equal(HttpStatusCode.OK, parkRes.StatusCode);
        var parkDto = await parkRes.Content.ReadFromJsonAsync<IngestEventResponse>();
        Assert.NotNull(parkDto);
        Assert.Equal(ParkingSessionStatus.Parked, parkDto.SessionStatus);

        // Step 3: Exit
        var exitRes = await _client.PostAsJsonAsync("/api/events", new IngestEventRequest
        {
            EventId = Guid.NewGuid().ToString(), SessionId = sessionId,
            ParkingLotId = 1, CarPlate = plate,
            EventType = ParkingEventType.VehicleExited,
            Source = "GateStaff", TriggeredBy = "gate_staff_01"
        });
        Assert.Equal(HttpStatusCode.OK, exitRes.StatusCode);
        var exitDto = await exitRes.Content.ReadFromJsonAsync<IngestEventResponse>();
        Assert.NotNull(exitDto);
        Assert.Equal(ParkingSessionStatus.Exited, exitDto.SessionStatus);
    }

    /// <summary>
    /// Endpoint danh sách phiên active phải trả 200.
    /// </summary>
    [Fact]
    public async Task GetActiveSessions_WithValidLotId_Returns200()
    {
        var response = await _client.GetAsync("/api/events/sessions/active?parkingLotId=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Endpoint nhật ký sự kiện phải trả 200.
    /// </summary>
    [Fact]
    public async Task GetEventLogs_WithValidLotId_Returns200()
    {
        var response = await _client.GetAsync("/api/events/logs?parkingLotId=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// SlotParked không có session → phải trả 400 với pending_verification.
    /// </summary>
    [Fact]
    public async Task PostEvent_SlotParked_WithUnknownSession_ReturnsBadRequestWithPendingVerification()
    {
        var request = new IngestEventRequest
        {
            EventId      = Guid.NewGuid().ToString(),
            SessionId    = Guid.NewGuid().ToString(), // unknown
            ParkingLotId = 1,
            SlotCode     = "B2-005",
            CarPlate     = "99A-IntTest",
            EventType    = ParkingEventType.SlotParked,
            Source       = "ZoneStaff",
            TriggeredBy  = "zone_staff_01"
        };

        var response = await _client.PostAsJsonAsync("/api/events", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<IngestEventResponse>();
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal(EventProcessingStatus.PendingVerification, result.ProcessingStatus);
    }

    // ─── Sprint 3 Day 6-9: 3D Visualization / ParkingSlots API smoke tests ───

    [Fact]
    public async Task GetParkingSlots_Returns200WithSlotsList()
    {
        var response = await _client.GetAsync("/api/ParkingSlots?lotId=1&floorId=B1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("slots", content);
        Assert.Contains("B1-A01", content);
    }

    [Fact]
    public async Task PatchParkingSlotStatus_UpdatesPhysicalStatus()
    {
        var updateRequest = new
        {
            physicalStatus = "OCCUPIED",
            carPlate = "51F-999.88"
        };

        var response = await _client.PatchAsJsonAsync("/api/ParkingSlots/B1-A02/status", updateRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("51F-999.88", content);
        Assert.Contains("OCCUPIED", content);
    }

    [Fact]
    public async Task GetParkingLotsMetadata_ReturnsAllThreeLotsWithFloorConfigurations()
    {
        var response = await _client.GetAsync("/api/ParkingSlots/lots");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Bãi 1", content);
        Assert.Contains("Bãi 2", content);
        Assert.Contains("Bãi 3", content);
    }
}
