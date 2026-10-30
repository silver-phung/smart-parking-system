using Microsoft.AspNetCore.Mvc;

namespace SmartParking.Api.Controllers;

/// <summary>Gives legacy viewer clients a clear response after mock slot data was removed.</summary>
[ApiController]
[Produces("application/problem+json")]
public sealed class LegacyParkingViewerController : ControllerBase
{
    [HttpGet("/api/ParkingSlots")]
    [HttpGet("/api/ParkingSlots/lots")]
    [HttpGet("/api/ParkingSlots/floor/{floorId}")]
    [HttpGet("/api/v1/parking-viewer/slots")]
    [HttpGet("/api/v1/parking-viewer/floors/{floorId}/slots")]
    public IActionResult ReadLegacyViewerRoute() => Problem(
        statusCode: StatusCodes.Status410Gone,
        title: "Nguồn dữ liệu viewer mô phỏng đã bị loại bỏ",
        detail: "Hãy đọc slot từ API core đã lưu trong PostgreSQL. Viewer không tự sinh bãi, slot hoặc xe mẫu.");

    [HttpPatch("/api/ParkingSlots/{slotCode}/status")]
    [HttpPatch("/api/v1/parking-viewer/slots/{slotCode}/status")]
    public IActionResult MutateLegacyViewerRoute() => Problem(
        statusCode: StatusCodes.Status410Gone,
        title: "Không được sửa trạng thái slot qua viewer",
        detail: "Trạng thái vật lý chỉ thay đổi qua API nghiệp vụ có guard và quyền phù hợp.");
}
