namespace SmartParking.Domain.Entities;

/// <summary>
/// Projection nội bộ của pipeline sự kiện. Đây không phải nguồn trạng thái core.
/// Ảnh/OCR tại cổng chỉ tạo phiên chờ xác minh; Staff điều phối mới chốt biển số và ô thực tế.
/// </summary>
public class ParkingSession
{
    public long Id { get; set; }

    /// <summary>UUID v4 do client sinh khi xe vào cổng – khóa nghiệp vụ duy nhất cho phiên.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>Biển số đã được Staff điều phối xác nhận. Rỗng khi mới nhận OCR tại cổng.</summary>
    public string CarPlate { get; set; } = string.Empty;

    /// <summary>Biển số OCR tạm thời, không dùng làm khóa nghiệp vụ trước khi xác minh.</summary>
    public string? RecognizedPlate { get; set; }

    public int ParkingLotId { get; set; }

    /// <summary>
    /// Mã ô đậu xe – format: [Khu][Cột]-[Số ô], ví dụ: "A1-012", "B2-005".
    /// Null khi xe mới vào cổng, chưa được Staff khu vực gán slot.
    /// </summary>
    public string? SlotCode { get; set; }

    /// <summary>
    /// Trạng thái phiên:
    ///   waiting_verification – đã nhận ảnh/OCR tại cổng, chờ xác minh tại slot.
    ///   parked              – Staff điều phối đã xác nhận xe và actual slot.
    ///   exit_processing     – đã nhận tín hiệu cổng ra; chưa chứng minh đủ phí/đã ra.
    /// </summary>
    public string Status { get; set; } = ParkingSessionStatus.WaitingVerification;

    /// <summary>
    /// Cảnh báo nghiệp vụ – null nếu không có vấn đề.
    /// "wrong_vehicle_type": xe xăng đậu ô điện hoặc ngược lại.
    /// </summary>
    public string? WarningCode { get; set; }

    /// <summary>URL ảnh xe Gemini AI chụp tại cổng vào (Object Storage).</summary>
    public string? EntryImageUrl { get; set; }

    /// <summary>URL ảnh xe tại cổng ra – đối chiếu lúc check-out.</summary>
    public string? ExitImageUrl { get; set; }

    /// <summary>Loại nhiên liệu đã được Staff xác nhận, không suy ra chỉ từ OCR/biển số.</summary>
    public string? FuelType { get; set; }

    /// <summary>Gợi ý nhiên liệu từ AI, chỉ để đối chiếu và không dùng quyết định tương thích.</summary>
    public string? RecognizedFuelType { get; set; }

    /// <summary>Màu xe Gemini nhận diện, ví dụ: "white", "black", "silver".</summary>
    public string? CarColor { get; set; }

    /// <summary>Hãng/Model xe Gemini nhận diện, ví dụ: "Toyota Vios", "VinFast VF8".</summary>
    public string? CarModel { get; set; }

    /// <summary>Staff điều phối xác minh xe và actual slot.</summary>
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
    public const string WaitingVerification = "waiting_verification";
    public const string Parked             = "parked";
    public const string ExitProcessing     = "exit_processing";
}

/// <summary>Hằng số cảnh báo nghiệp vụ trên phiên.</summary>
public static class ParkingSessionWarning
{
    public const string WrongVehicleType = "wrong_vehicle_type";
}
