using Microsoft.AspNetCore.Mvc;

namespace SmartParking.Api.Controllers;

/// <summary>
/// Giữ route viewer cũ để client nhận lỗi chuyển đổi rõ ràng.
/// Nguồn dictionary/random và PATCH trạng thái trực tiếp đã bị loại khỏi luồng vận hành.
/// Viewer phải đọc API slot core từ PostgreSQL, ví dụ
/// GET /api/v1/parking-floors/{floorId}/slots.
/// </summary>
[ApiController]
[Produces("application/problem+json")]
public sealed class LegacyParkingViewerController : ControllerBase
{
    [HttpGet("/api/ParkingSlots")]
    [HttpGet("/api/ParkingSlots/lots")]
    [HttpGet("/api/ParkingSlots/floor/{floorId}")]
    [HttpGet("/api/v1/parking-viewer/slots")]
    [HttpGet("/api/v1/parking-viewer/floors/{floorId}/slots")]
    public IActionResult ReadLegacyViewerRoute()
    {
        return Problem(
            statusCode: StatusCodes.Status410Gone,
            title: "Nguồn dữ liệu viewer mô phỏng đã bị loại bỏ",
            detail: "Hãy đọc slot từ API core đã lưu trong PostgreSQL. Viewer không tự sinh bãi, slot hoặc xe mẫu.");
    }

    [HttpPatch("/api/ParkingSlots/{slotCode}/status")]
    [HttpPatch("/api/v1/parking-viewer/slots/{slotCode}/status")]
    public IActionResult MutateLegacyViewerRoute()
    {
        return Problem(
            statusCode: StatusCodes.Status410Gone,
            title: "Không được sửa trạng thái slot qua viewer",
            detail: "Trạng thái vật lý chỉ thay đổi qua API nghiệp vụ có guard và quyền phù hợp.");
    }
}
