using System.ComponentModel.DataAnnotations;
using SmartParking.Domain.Entities;

namespace SmartParking.Application.Events.Dtos;

// ─────────────────────────────────────────────────────────────────────────────
// INPUT – nhận request từ Camera AI, IoT Sensor, Staff App
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Payload chuẩn gửi sự kiện vào Ingestion API.
///
/// Quy tắc EventId (Idempotency):
///   • Mỗi lần bấm nút / gửi request MỚI → sinh UUID v4 MỚI.
///   • Nếu mạng timeout và phải retry → dùng đúng UUID cũ.
///   • Cùng EventId gửi nhiều lần → hệ thống trả kết quả cũ, không xử lý đúp.
///
/// Quy tắc SessionId:
///   • Sinh 1 UUID khi xe VÀO CỔNG lần đầu; tất cả sự kiện trong cùng lượt dùng chung.
///   • Null với VehicleEntered (server tự tạo nếu client không cung cấp).
///   • Bắt buộc với SlotParked và VehicleExited.
/// </summary>
public sealed class IngestEventRequest
{
    [Required(ErrorMessage = "EventId là bắt buộc.")]
    [RegularExpression("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-4[0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$",
        ErrorMessage = "EventId phải là UUID v4.")]
    public string EventId { get; init; } = string.Empty;

    [Range(1, 1, ErrorMessage = "PayloadVersion hiện chỉ hỗ trợ phiên bản 1.")]
    public int PayloadVersion { get; init; } = 1;

    public string? SessionId { get; init; }

    [Required, Range(1, int.MaxValue, ErrorMessage = "ParkingLotId phải lớn hơn 0.")]
    public int ParkingLotId { get; init; }

    /// <summary>
    /// Mã ô đậu xe – bắt buộc với SlotParked.
    /// Format: [Khu][Cột]-[Số ô], ví dụ: "A1-012", "B2-005", "C3-099".
    /// </summary>
    [RegularExpression(@"^[A-Z]\d{1,2}-\d{3}$",
        ErrorMessage = "SlotCode sai định dạng. Ví dụ hợp lệ: A1-012, B2-005.")]
    public string? SlotCode { get; init; }

    /// <summary>
    /// Với VehicleEntered đây là OCR tạm. Với SlotParked đây là biển số Staff điều phối xác nhận.
    /// Không dùng OCR tạm làm khóa duy nhất của phiên.
    /// </summary>
    public string? CarPlate { get; init; }

    /// <summary>
    /// Loại sự kiện – dùng hằng số trong <see cref="ParkingEventType"/>:
    /// VehicleEntered | SensorTriggered | SlotParked | VehicleExited.
    /// </summary>
    [Required(ErrorMessage = "EventType là bắt buộc.")]
    public string EventType { get; init; } = string.Empty;

    /// <summary>Nguồn gốc, ví dụ: "CameraGate01", "IoTSensor_A1-012", "GateStaff", "ZoneStaff".</summary>
    [Required(ErrorMessage = "Source là bắt buộc.")]
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// Giá trị legacy do client khai báo, chỉ lưu để chẩn đoán và không dùng làm actor có quyền.
    /// Actor chính thức phải được truyền từ ngữ cảnh xác thực vào service.
    /// </summary>
    [Obsolete("Không dùng TriggeredBy từ body để phân quyền.")]
    public string? TriggeredBy { get; init; }

    /// <summary>Thời điểm sự kiện xảy ra (UTC). Null → server dùng thời gian nhận.</summary>
    public DateTimeOffset? EventTimestampUtc { get; init; }

    // ── Thông tin Gemini AI (chỉ có với VehicleEntered) ──────────────────────

    /// <summary>URL ảnh xe tại cổng vào đã upload lên Object Storage.</summary>
    public string? EntryImageUrl { get; init; }

    /// <summary>Gợi ý AI ở cổng hoặc giá trị Staff xác nhận tại slot, tùy EventType.</summary>
    public string? FuelType { get; init; }

    /// <summary>Màu xe, ví dụ: "white", "black", "silver".</summary>
    public string? CarColor { get; init; }

    /// <summary>Hãng/Model xe, ví dụ: "Toyota Vios", "VinFast VF8".</summary>
    public string? CarModel { get; init; }

    /// <summary>URL ảnh xe tại cổng ra – chỉ dùng với VehicleExited để đối chiếu.</summary>
    public string? ExitImageUrl { get; init; }
}

// ─────────────────────────────────────────────────────────────────────────────
// OUTPUT – trả về client sau khi xử lý
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Kết quả xử lý sự kiện trả về cho client.</summary>
public sealed class IngestEventResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;

    /// <summary>"accepted" | "rejected" | "duplicate".</summary>
    public string ProcessingStatus { get; init; } = string.Empty;

    /// <summary>True khi sự kiện đã được xử lý từ trước (cùng EventId).</summary>
    public bool IsDuplicate { get; init; }

    public string EventId { get; init; } = string.Empty;
    public string? SessionId { get; init; }

    /// <summary>Trạng thái projection: waiting_verification | parked | exit_processing.</summary>
    public string? SessionStatus { get; init; }

    /// <summary>Cảnh báo nghiệp vụ nếu có, ví dụ: "wrong_vehicle_type".</summary>
    public string? WarningCode { get; init; }

    public string? WarningMessage { get; init; }
    public DateTimeOffset ProcessedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Thông tin tóm tắt một phiên đang hoạt động – dùng cho màn hình danh sách.</summary>
public sealed class ActiveSessionDto
{
    public string SessionId { get; init; } = string.Empty;
    public string CarPlate { get; init; } = string.Empty;
    public string? RecognizedPlate { get; init; }
    public int ParkingLotId { get; init; }
    public string? SlotCode { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? WarningCode { get; init; }
    public string? FuelType { get; init; }
    public string? CarColor { get; init; }
    public string? CarModel { get; init; }
    public string? EntryImageUrl { get; init; }
    public DateTimeOffset EnteredAtUtc { get; init; }
    public DateTimeOffset? ParkedAtUtc { get; init; }
}

/// <summary>Bản ghi sự kiện trong nhật ký – dùng cho màn hình giám sát và debug.</summary>
public sealed class EventLogDto
{
    public long Id { get; init; }
    public string EventId { get; init; } = string.Empty;
    public string? SessionId { get; init; }
    public int ParkingLotId { get; init; }
    public string? SlotCode { get; init; }
    public string CarPlate { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string? TriggeredBy { get; init; }
    public int PayloadVersion { get; init; }
    public string ProcessingStatus { get; init; } = string.Empty;
    public string? RejectReason { get; init; }
    public DateTimeOffset EventTimestampUtc { get; init; }
    public DateTimeOffset ReceivedAtUtc { get; init; }
}
