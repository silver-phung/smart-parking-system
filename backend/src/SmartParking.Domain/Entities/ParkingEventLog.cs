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

    /// <summary>ID nhân viên kích hoạt (nếu là thao tác của Staff).</summary>
    public string? TriggeredBy { get; set; }

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

    /// <summary>Sự kiện đến không đúng thứ tự – chờ xác minh thủ công.</summary>
    public const string PendingVerification = "pending_verification";
}

/// <summary>Hằng số loại sự kiện IoT/Staff.</summary>
public static class ParkingEventType
{
    /// <summary>Xe qua cổng vào – Camera + Gemini AI phân tích, Staff cổng xác nhận.</summary>
    public const string VehicleEntered  = "VehicleEntered";

    /// <summary>Cảm biến siêu âm tại ô phát hiện có xe – chưa xác định xe nào.</summary>
    public const string SensorTriggered = "SensorTriggered";

    /// <summary>Staff khu vực gán xe vào ô sau khi cảm biến báo.</summary>
    public const string SlotParked      = "SlotParked";

    /// <summary>Xe ra cổng – Staff đối chiếu ảnh + đóng phiên.</summary>
    public const string VehicleExited   = "VehicleExited";
}
