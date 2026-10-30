using System.ComponentModel.DataAnnotations;
using SmartParking.Domain.Constants;

namespace SmartParking.Application.DTOs.Parking;

public class ParkingSlotDto
{
    public long Id { get; set; }
    public int FloorId { get; set; }
    public string FloorName { get; set; } = null!;
    public int LotId { get; set; }
    public string LotName { get; set; } = null!;
    public string SlotCode { get; set; } = null!;
    public string PhysicalStatus { get; set; } = null!;
    public bool IsAvailable { get; set; }
    public decimal? PositionX { get; set; }
    public decimal? PositionY { get; set; }
    public decimal? PositionZ { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ParkingSlotDetailDto : ParkingSlotDto
{
    /// <summary>
    /// Biển số xe hiện tại đang đỗ hoặc được phân công (Chỉ hiển thị cho Quản trị viên và Nhân viên nội bộ).
    /// </summary>
    public string? CurrentVehiclePlate { get; set; }

    /// <summary>
    /// ID phiên gửi xe đang sử dụng ô đỗ (nếu có, chỉ nội bộ).
    /// </summary>
    public long? CurrentSessionId { get; set; }
}

public class CreateParkingSlotRequestDto
{
    [Required(ErrorMessage = "Mã ô đỗ là bắt buộc.")]
    [StringLength(20, MinimumLength = 1, ErrorMessage = "Mã ô đỗ phải từ 1 đến 20 ký tự.")]
    public string SlotCode { get; set; } = null!;

    public string PhysicalStatus { get; set; } = SlotPhysicalStatuses.Empty;

    public decimal? PositionX { get; set; }
    public decimal? PositionY { get; set; }
    public decimal? PositionZ { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateParkingSlotRequestDto
{
    [Required(ErrorMessage = "Mã ô đỗ là bắt buộc.")]
    [StringLength(20, MinimumLength = 1, ErrorMessage = "Mã ô đỗ phải từ 1 đến 20 ký tự.")]
    public string SlotCode { get; set; } = null!;

    public decimal? PositionX { get; set; }
    public decimal? PositionY { get; set; }
    public decimal? PositionZ { get; set; }

    public bool IsActive { get; set; }
}

public class UpdateSlotPhysicalStatusRequestDto
{
    [Required(ErrorMessage = "Trạng thái vật lý của ô đỗ là bắt buộc.")]
    public string PhysicalStatus { get; set; } = null!;

    [StringLength(255, ErrorMessage = "Lý do cập nhật tối đa 255 ký tự.")]
    public string? Reason { get; set; }
}

public class ParkingSlotFilterDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Số trang phải lớn hơn 0.")]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Số lượng bản ghi mỗi trang từ 1 đến 100.")]
    public int PageSize { get; set; } = 20;

    public string? PhysicalStatus { get; set; }

    public bool? IsActive { get; set; }

    public string? Search { get; set; }
}
