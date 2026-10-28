namespace SmartParking.Domain.Entities;

/// <summary>
/// Phiên gửi xe – mỗi lượt xe vào/ra tạo ra 1 phiên.
/// Phiên được mở khi xe qua cổng vào (VehicleEntered) và đóng khi xe ra (VehicleExited).
/// </summary>
public class EventParkingSession
{
    public long Id { get; set; }

    /// <summary>UUID v4 do client sinh khi xe vào cổng – khóa nghiệp vụ duy nhất cho phiên.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>Biển số xe, ví dụ: "51F-67890". Đã chuẩn hóa thành chữ HOA.</summary>
    public string CarPlate { get; set; } = string.Empty;

    public int ParkingLotId { get; set; }

    /// <summary>
    /// Mã ô đậu xe – format: [Khu][Cột]-[Số ô], ví dụ: "A1-012", "B2-005".
    /// Null khi xe mới vào cổng, chưa được Staff khu vực gán slot.
    /// </summary>
    public string? SlotCode { get; set; }

    /// <summary>
    /// Trạng thái phiên:
    ///   pending_slot – xe đã vào cổng, chờ cảm biến IoT + Staff gán slot.
    ///   parked        – Staff đã gán slot, xe đang đậu.
    ///   exited        – xe đã ra cổng, phiên đóng.
    /// </summary>
    public string Status { get; set; } = ParkingSessionStatus.PendingSlot;

    /// <summary>
    /// Cảnh báo nghiệp vụ – null nếu không có vấn đề.
    /// "wrong_vehicle_type": xe xăng đậu ô điện hoặc ngược lại.
    /// </summary>
    public string? WarningCode { get; set; }

    /// <summary>URL ảnh xe Gemini AI chụp tại cổng vào (Object Storage).</summary>
    public string? EntryImageUrl { get; set; }

    /// <summary>URL ảnh xe tại cổng ra – đối chiếu lúc check-out.</summary>
    public string? ExitImageUrl { get; set; }

    /// <summary>Loại nhiên liệu Gemini nhận diện: "gasoline" | "electric" | "unknown".</summary>
    public string? FuelType { get; set; }

    /// <summary>Màu xe Gemini nhận diện, ví dụ: "white", "black", "silver".</summary>
    public string? CarColor { get; set; }

    /// <summary>Hãng/Model xe Gemini nhận diện, ví dụ: "Toyota Vios", "VinFast VF8".</summary>
    public string? CarModel { get; set; }

    /// <summary>Staff cổng vào thực hiện check-in.</summary>
    public string? GateStaffId { get; set; }

    /// <summary>Staff khu vực gán slot.</summary>
    public string? ZoneStaffId { get; set; }

    public DateTimeOffset EnteredAtUtc { get; set; }
    public DateTimeOffset? ParkedAtUtc { get; set; }
    public DateTimeOffset? ExitedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Hằng số trạng thái phiên gửi xe.</summary>
public static class ParkingSessionStatus
{
    public const string PendingSlot = "pending_slot";
    public const string Parked      = "parked";
    public const string Exited      = "exited";
}

/// <summary>Hằng số cảnh báo nghiệp vụ trên phiên.</summary>
public static class ParkingSessionWarning
{
    public const string WrongVehicleType = "wrong_vehicle_type";
}
