using System.ComponentModel.DataAnnotations;

namespace SmartParking.Application.DTOs.Sessions;

public class ParkingSessionDto
{
    public long Id { get; set; }
    public int LotId { get; set; }
    public string LotName { get; set; } = string.Empty;
    public long? VehicleId { get; set; }
    public long? BookingId { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public DateTime CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public long? CurrentSlotId { get; set; }
    public string? SlotCode { get; set; }
    public int? FloorNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalFee { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? CheckInImageUrl { get; set; }
    public string? CheckOutImageUrl { get; set; }
    public double? DurationMinutes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CheckInRequestDto
{
    [Required(ErrorMessage = "Bãi đỗ xe không được để trống.")]
    [Range(1, int.MaxValue, ErrorMessage = "ID bãi đỗ xe không hợp lệ.")]
    public int LotId { get; set; }

    [Required(ErrorMessage = "Biển số xe không được để trống.")]
    [StringLength(20, MinimumLength = 3, ErrorMessage = "Biển số xe phải từ 3 đến 20 ký tự.")]
    public string PlateNumber { get; set; } = string.Empty;

    public long? SlotId { get; set; }

    [MaxLength(500, ErrorMessage = "URL hình ảnh check-in không được vượt quá 500 ký tự.")]
    public string? CheckInImageUrl { get; set; }
}

public class CheckOutRequestDto
{
    [MaxLength(500, ErrorMessage = "URL hình ảnh check-out không được vượt quá 500 ký tự.")]
    public string? CheckOutImageUrl { get; set; }

    [MaxLength(20, ErrorMessage = "Biển số xe xác nhận xuất bãi không được vượt quá 20 ký tự.")]
    public string? ConfirmedPlateNumber { get; set; }

    public bool? AllowExitWithoutPayment { get; set; }

    [MaxLength(500, ErrorMessage = "Lý do miễn trừ thanh toán không được vượt quá 500 ký tự.")]
    public string? ExemptionReason { get; set; }
}

public class PaymentClearanceRequestDto
{
    [Required(ErrorMessage = "Số tiền thanh toán là bắt buộc.")]
    [Range(0, 100_000_000, ErrorMessage = "Số tiền thanh toán không hợp lệ (phải từ 0 đến 100,000,000 VNĐ).")]
    public decimal AmountPaid { get; set; }

    [Required(ErrorMessage = "Mã tham chiếu giao dịch (TransactionRef) là bắt buộc.")]
    [MaxLength(100, ErrorMessage = "Mã tham chiếu giao dịch không được vượt quá 100 ký tự.")]
    public string TransactionRef { get; set; } = string.Empty;

    [MaxLength(50, ErrorMessage = "Phương thức thanh toán không được vượt quá 50 ký tự.")]
    public string? PaymentMethod { get; set; }
}

public class ParkingSessionFilterDto
{
    public int? LotId { get; set; }
    public string? PlateNumber { get; set; }
    public string? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class VerifySessionSlotRequestDto
{
    [Required(ErrorMessage = "Vị trí đỗ xe không được để trống.")]
    [Range(1, long.MaxValue, ErrorMessage = "ID vị trí đỗ xe không hợp lệ.")]
    public long SlotId { get; set; }

    [MaxLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự.")]
    public string? Notes { get; set; }
}

public class ReportWrongSlotSessionRequestDto
{
    [Required(ErrorMessage = "Vị trí đỗ xe thực tế không được để trống.")]
    [Range(1, long.MaxValue, ErrorMessage = "ID vị trí đỗ xe thực tế không hợp lệ.")]
    public long ActualSlotId { get; set; }

    [MaxLength(500, ErrorMessage = "Lý do/ghi chú không được vượt quá 500 ký tự.")]
    public string? Reason { get; set; }
}

public class UnmatchedSessionDto
{
    public long SessionId { get; set; }
    public int LotId { get; set; }
    public string LotName { get; set; } = string.Empty;
    public string PlateNumber { get; set; } = string.Empty;
    public DateTime CheckInTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CheckInImageUrl { get; set; }
    public long? BookingId { get; set; }
}

public class UnmatchedSlotDto
{
    public long SlotId { get; set; }
    public int LotId { get; set; }
    public string LotName { get; set; } = string.Empty;
    public string SlotCode { get; set; } = string.Empty;
    public int FloorNumber { get; set; }
    public string PhysicalStatus { get; set; } = string.Empty;
    public DateTime? LastStatusChangedAt { get; set; }
}
