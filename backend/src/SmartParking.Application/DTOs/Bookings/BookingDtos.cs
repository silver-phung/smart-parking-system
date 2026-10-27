using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SmartParking.Application.DTOs.Bookings;

public class BookingDto
{
    public long Id { get; set; }
    public int LotId { get; set; }
    public string LotName { get; set; } = null!;
    public string? LotAddress { get; set; }
    public long UserId { get; set; }
    public string Username { get; set; } = null!;
    public long VehicleId { get; set; }
    public string PlateNumber { get; set; } = null!;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string Status { get; set; } = null!;
    public long? SessionId { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class BookingDetailDto : BookingDto
{
    public string? VehicleBrand { get; set; }
    public string? VehicleModel { get; set; }
    public string? VehicleColor { get; set; }
}

public class CreateBookingRequestDto
{
    [Required(ErrorMessage = "Bãi đỗ xe không được để trống")]
    public int LotId { get; set; }

    public long? VehicleId { get; set; }

    [MaxLength(20, ErrorMessage = "Biển số xe không được vượt quá 20 ký tự")]
    public string? PlateNumber { get; set; }

    [Required(ErrorMessage = "Thời gian bắt đầu đặt chỗ không được để trống")]
    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    [MaxLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự")]
    public string? Notes { get; set; }
}

public class CancelBookingRequestDto
{
    [MaxLength(500, ErrorMessage = "Lý do hủy không được vượt quá 500 ký tự")]
    public string? Reason { get; set; }
}

public class BookingFilterDto
{
    public int? LotId { get; set; }
    public string? Status { get; set; }
    public string? Search { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class LotCapacityDto
{
    public int LotId { get; set; }
    public string LotName { get; set; } = null!;
    public string? LotAddress { get; set; }
    public int TotalCapacity { get; set; }
    public int AvailableForWalkIn { get; set; }
    public int AvailableForBooking { get; set; }
    public bool IsFull { get; set; }

    // Operational diagnostics (available to STAFF/MANAGER/ADMIN, omitted when null for anonymous public)
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ActiveSessionsCount { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ActiveReservationsCount { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TotalOccupiedOrReserved { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PhysicalOccupiedSlotsCount { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PhysicalEmptySlotsCount { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DiscrepancyNotes { get; set; }
}
