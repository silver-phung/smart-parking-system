using System.ComponentModel.DataAnnotations;

namespace SmartParking.Application.DTOs.Parking;

public class ParkingLotDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Address { get; set; }
    public int TotalCapacity { get; set; }
    public int TotalFloors { get; set; }
    public int AvailableSlotsCount { get; set; }
    public int OccupiedSlotsCount { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ParkingLotDetailDto : ParkingLotDto
{
    public IReadOnlyList<ParkingFloorSummaryDto> Floors { get; set; } = [];
}

public class CreateParkingLotRequestDto
{
    [Required(ErrorMessage = "Tên bãi đỗ xe là bắt buộc.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Tên bãi đỗ xe phải từ 2 đến 100 ký tự.")]
    public string Name { get; set; } = null!;

    [StringLength(255, ErrorMessage = "Địa chỉ bãi đỗ xe tối đa 255 ký tự.")]
    public string? Address { get; set; }

    [Range(0, 100000, ErrorMessage = "Sức chứa bãi đỗ xe phải từ 0 đến 100.000.")]
    public int TotalCapacity { get; set; } = 0;

    public bool IsActive { get; set; } = true;
}

public class UpdateParkingLotRequestDto
{
    [Required(ErrorMessage = "Tên bãi đỗ xe là bắt buộc.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Tên bãi đỗ xe phải từ 2 đến 100 ký tự.")]
    public string Name { get; set; } = null!;

    [StringLength(255, ErrorMessage = "Địa chỉ bãi đỗ xe tối đa 255 ký tự.")]
    public string? Address { get; set; }

    [Range(0, 100000, ErrorMessage = "Sức chứa bãi đỗ xe phải từ 0 đến 100.000.")]
    public int TotalCapacity { get; set; }

    public bool IsActive { get; set; }
}

public class UpdateLotStatusRequestDto
{
    [Required(ErrorMessage = "Trạng thái hoạt động là bắt buộc.")]
    public bool IsActive { get; set; }

    [StringLength(255, ErrorMessage = "Lý do thay đổi trạng thái tối đa 255 ký tự.")]
    public string? Reason { get; set; }
}

public class ParkingLotFilterDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Số trang phải lớn hơn 0.")]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Số lượng bản ghi mỗi trang từ 1 đến 100.")]
    public int PageSize { get; set; } = 10;

    public bool? IsActive { get; set; }

    public string? Search { get; set; }
}
