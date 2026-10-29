namespace SmartParking.Domain.Entities;

/// <summary>
/// Nhật ký tất cả sự kiện IoT/Staff gửi vào hệ thống.
/// Append-only – không cập nhật hay xóa sau khi tạo.
/// Phục vụ: truy vết, chống xử lý trùng (Idempotency), đối chiếu tranh chấp.
/// </summary>
public class ParkingEventLog
{
    public long Id { get; set; }

    /// <summary>
    /// UUID v4 do client sinh – khóa idempotency.
    /// Ràng buộc UNIQUE tại DB: cùng EventId gửi nhiều lần → chỉ xử lý 1 lần.
    /// </summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>SessionId của phiên liên quan. Null nếu sự kiện bị reject trước khi xác định phiên.</summary>
    public string? SessionId { get; set; }

    public int ParkingLotId { get; set; }

    /// <summary>Mã ô liên quan (nếu có), format: "A1-012". Null với VehicleEntered.</summary>
    public string? SlotCode { get; set; }

    /// <summary>Biển số xe, ví dụ: "51F-67890".</summary>
    public string CarPlate { get; set; } = string.Empty;

    /// <summary>
    /// Loại sự kiện – dùng hằng số trong <see cref="ParkingEventType"/>:
    /// VehicleEntered | SensorTriggered | SlotParked | VehicleExited.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Nguồn gốc sự kiện, ví dụ: "CameraGate01", "IoTSensor_A1-012", "GateStaff".</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>ID actor lấy từ ngữ cảnh xác thực, không lấy từ body request.</summary>
    public string? TriggeredBy { get; set; }

    /// <summary>Phiên bản payload để consumer xử lý tương thích.</summary>
    public int PayloadVersion { get; set; } = 1;

    /// <summary>
    /// Kết quả xử lý:
    ///   accepted  – hợp lệ, đã xử lý nghiệp vụ.
    ///   rejected  – không hợp lệ (ví dụ: xe chưa check-in mà đã checkout).
    ///   duplicate – trùng EventId, bỏ qua, trả kết quả cũ.
    /// </summary>
    public string ProcessingStatus { get; set; } = EventProcessingStatus.Accepted;

    /// <summary>Lý do từ chối – có giá trị khi ProcessingStatus = "rejected".</summary>
    public string? RejectReason { get; set; }

    /// <summary>Thời điểm sự kiện xảy ra thực tế (do thiết bị/client cung cấp).</summary>
    public DateTimeOffset EventTimestampUtc { get; set; }

    /// <summary>Thời điểm server nhận và ghi nhật ký.</summary>
    public DateTimeOffset ReceivedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Hằng số trạng thái xử lý sự kiện.</summary>
public static class EventProcessingStatus
{
    public const string Accepted  = "accepted";
    public const string Rejected  = "rejected";
    public const string Duplicate = "duplicate";
}

/// <summary>Hằng số loại sự kiện IoT/Staff.</summary>
public static class ParkingEventType
{
    /// <summary>Camera/AI ghi nhận xe qua cổng; OCR vẫn là dữ liệu tạm.</summary>
    public const string VehicleEntered  = "VehicleEntered";

    /// <summary>Cảm biến siêu âm tại ô phát hiện có xe – chưa xác định xe nào.</summary>
    public const string SensorTriggered = "SensorTriggered";

    /// <summary>Staff điều phối xác minh biển số và actual slot.</summary>
    public const string SlotParked      = "SlotParked";

    /// <summary>Tín hiệu xe tới cổng ra; không tự đóng phiên hoặc trả ô.</summary>
    public const string VehicleExited   = "VehicleExited";
}
