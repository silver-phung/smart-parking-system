using System.ComponentModel.DataAnnotations;

namespace SmartParking.Application.DTOs.Parking;

public class ParkingFloorDto
{
    public int Id { get; set; }
    public int LotId { get; set; }
    public string LotName { get; set; } = null!;
    public int FloorNumber { get; set; }
    public string Name { get; set; } = null!;
    public int TotalSlots { get; set; }
    public int AvailableSlotsCount { get; set; }
    public int OccupiedSlotsCount { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ParkingFloorSummaryDto
{
    public int Id { get; set; }
    public int FloorNumber { get; set; }
    public string Name { get; set; } = null!;
    public int TotalSlots { get; set; }
    public int AvailableSlotsCount { get; set; }
    public int OccupiedSlotsCount { get; set; }
    public bool IsActive { get; set; }
}

public class CreateParkingFloorRequestDto
{
    [Required(ErrorMessage = "Số thứ tự tầng là bắt buộc.")]
    [Range(-10, 100, ErrorMessage = "Số thứ tự tầng phải từ -10 (tầng hầm) đến 100.")]
    public int FloorNumber { get; set; }

    [Required(ErrorMessage = "Tên tầng là bắt buộc.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Tên tầng phải từ 1 đến 50 ký tự.")]
    public string Name { get; set; } = null!;

    [Range(0, 10000, ErrorMessage = "Tổng số ô đỗ của tầng phải từ 0 đến 10.000.")]
    public int TotalSlots { get; set; } = 0;

    public bool IsActive { get; set; } = true;
}

public class UpdateParkingFloorRequestDto
{
    [Required(ErrorMessage = "Số thứ tự tầng là bắt buộc.")]
    [Range(-10, 100, ErrorMessage = "Số thứ tự tầng phải từ -10 đến 100.")]
    public int FloorNumber { get; set; }

    [Required(ErrorMessage = "Tên tầng là bắt buộc.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Tên tầng phải từ 1 đến 50 ký tự.")]
    public string Name { get; set; } = null!;

    [Range(0, 10000, ErrorMessage = "Tổng số ô đỗ của tầng phải từ 0 đến 10.000.")]
    public int TotalSlots { get; set; }

    public bool IsActive { get; set; }
}
