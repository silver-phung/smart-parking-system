using System.ComponentModel.DataAnnotations;

namespace SmartParking.Application.DTOs.Dispatches;

public class ParkingDispatchDto
{
    public long Id { get; set; }
    public long SessionId { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public int LotId { get; set; }
    public string LotName { get; set; } = string.Empty;

    // Phân công ô đỗ được chỉ định
    public long AssignedSlotId { get; set; }
    public string AssignedSlotCode { get; set; } = string.Empty;
    public int AssignedFloorNumber { get; set; }
    public string AssignedFloorName { get; set; } = string.Empty;

    // Tọa độ 3D phục vụ mô hình 3D Visualization
    public decimal? AssignedPositionX { get; set; }
    public decimal? AssignedPositionY { get; set; }
    public decimal? AssignedPositionZ { get; set; }

    // Ô đỗ thực tế xe đỗ (trong trường hợp đỗ đúng ô hoặc đỗ sai ô WRONG_SLOT)
    public long? ActualSlotId { get; set; }
    public string? ActualSlotCode { get; set; }
    public int? ActualFloorNumber { get; set; }
    public decimal? ActualPositionX { get; set; }
    public decimal? ActualPositionY { get; set; }
    public decimal? ActualPositionZ { get; set; }

    public string Status { get; set; } = string.Empty;
    public string DispatchMode { get; set; } = string.Empty;
    public DateTime DispatchedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateDispatchRequestDto
{
    [Required(ErrorMessage = "Mã phiên đỗ xe (SessionId) không được để trống.")]
    [Range(1, long.MaxValue, ErrorMessage = "Mã phiên đỗ xe không hợp lệ.")]
    public long SessionId { get; set; }

    /// <summary>
    /// ID ô đỗ muốn chỉ định. Nếu để trống, hệ thống sẽ tự động tìm ô đỗ khả dụng đầu tiên trong bãi (Auto-dispatch).
    /// </summary>
    public long? AssignedSlotId { get; set; }

    /// <summary>
    /// Chế độ điều phối: AUTO hoặc MANUAL. Nếu AssignedSlotId được truyền vào thì mặc định là MANUAL.
    /// </summary>
    public string? DispatchMode { get; set; }

    [MaxLength(255, ErrorMessage = "Ghi chú không được vượt quá 255 ký tự.")]
    public string? Notes { get; set; }
}

public class AssignSlotRequestDto
{
    [Required(ErrorMessage = "Mã ô đỗ mới không được để trống.")]
    [Range(1, long.MaxValue, ErrorMessage = "Mã ô đỗ không hợp lệ.")]
    public long SlotId { get; set; }

    [MaxLength(255, ErrorMessage = "Ghi chú không được vượt quá 255 ký tự.")]
    public string? Notes { get; set; }
}

public class ConfirmOccupiedRequestDto
{
    [MaxLength(255, ErrorMessage = "Ghi chú xác nhận không được vượt quá 255 ký tự.")]
    public string? Notes { get; set; }
}

public class ReportWrongSlotRequestDto
{
    [Required(ErrorMessage = "Mã ô đỗ thực tế xe đang đỗ sai không được để trống.")]
    [Range(1, long.MaxValue, ErrorMessage = "Mã ô đỗ thực tế không hợp lệ.")]
    public long ActualSlotId { get; set; }

    [MaxLength(255, ErrorMessage = "Ghi chú lý do đỗ sai không được vượt quá 255 ký tự.")]
    public string? Notes { get; set; }
}

public class CancelDispatchRequestDto
{
    [MaxLength(255, ErrorMessage = "Lý do hủy không được vượt quá 255 ký tự.")]
    public string? Reason { get; set; }
}

public class ParkingDispatchFilterDto
{
    public int? LotId { get; set; }
    public long? SessionId { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
