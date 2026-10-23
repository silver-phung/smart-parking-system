using System.ComponentModel.DataAnnotations;

namespace SmartParking.Application.DTOs.Sessions;

public class ParkingSessionDto
{
    public long Id { get; set; }
    public int LotId { get; set; }
    public string LotName { get; set; } = string.Empty;
    public long? VehicleId { get; set; }
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
