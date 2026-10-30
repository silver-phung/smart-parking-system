namespace SmartParking.Application.Events.Dtos;

/// <summary>
/// DTO đại diện cho một ô đỗ xe trong bãi (phục vụ render 2D/3D và quản lý).
/// </summary>
public class ParkingSlotDto
{
    public long Id { get; set; }
    public int LotId { get; set; }
    public string LotName { get; set; } = string.Empty;
    public string FloorId { get; set; } = string.Empty;
    public string FloorName { get; set; } = string.Empty;

    /// <summary>
    /// Mã ô đỗ theo chuẩn thực tế, ví dụ: "A1-012", "B1-A01".
    /// </summary>
    public string SlotCode { get; set; } = string.Empty;

    /// <summary>
    /// Trạng thái vật lý của ô:
    ///   EMPTY        (🟢 Trống / Available)
    ///   OCCUPIED     (🔴 Có xe đã đỗ)
    ///   RESERVED     (⏳ Đã phân chỗ nhưng xe chưa vào bãi/vào ô)
    ///   MAINTENANCE  (🟡 Bảo trì)
    ///   UNAVAILABLE  (⚪ Không khả dụng / Tạm khóa)
    /// </summary>
    public string PhysicalStatus { get; set; } = "EMPTY";

    /// <summary>
    /// Loại phương tiện: "car" (ô tô), "ev" (ô tô điện), "moto" (xe máy).
    /// </summary>
    public string VehicleType { get; set; } = "car";

    /// <summary>
    /// Biển số xe hiện tại đang đỗ hoặc được phân công (nếu có).
    /// </summary>
    public string? CurrentCarPlate { get; set; }

    /// <summary>
    /// Hãng/Model xe hoặc màu xe nhận diện bởi AI (nếu có).
    /// </summary>
    public string? VehicleModel { get; set; }
    public string? VehicleColor { get; set; }

    /// <summary>
    /// SessionId của phiên gửi xe tương ứng.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Thời điểm bắt đầu đỗ (nếu đã đỗ).
    /// </summary>
    public DateTimeOffset? ParkedSinceUtc { get; set; }

    /// <summary>
    /// Tọa độ không gian 3D tương đối trong mặt bằng tầng (mét).
    /// </summary>
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public double PositionZ { get; set; }

    /// <summary>
    /// Góc quay đỗ xe (độ: 0, 90, 180, 270 hoặc góc chéo 45 độ).
    /// </summary>
    public double RotationY { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public class ReserveSlotRequest
{
    public string? CarPlate { get; set; }
    public string? VehicleModel { get; set; }
    public string? VehicleColor { get; set; }
}

public class UpdateSlotStatusRequest
{
    public string PhysicalStatus { get; set; } = "EMPTY";
    public string? CarPlate { get; set; }
    public string? Reason { get; set; }
}
