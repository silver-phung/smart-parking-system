using System.ComponentModel.DataAnnotations;
using SmartParking.Domain.Constants;

namespace SmartParking.Application.DTOs.StaffAssignments;

public class StaffLotAssignmentDto
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Username { get; set; } = null!;
    public string UserFullName { get; set; } = null!;
    public string UserEmail { get; set; } = null!;
    public string UserRole { get; set; } = null!;

    public int LotId { get; set; }
    public string LotName { get; set; } = null!;

    public long AssignedBy { get; set; }
    public string AssignedByUsername { get; set; } = null!;
    public DateTime AssignedAt { get; set; }

    public bool IsActive { get; set; }
    public string Duty { get; set; } = StaffDuties.Gate;

    public long? RevokedBy { get; set; }
    public string? RevokedByUsername { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevocationReason { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateStaffAssignmentRequestDto
{
    [Required(ErrorMessage = "Mã người dùng (userId) là bắt buộc.")]
    [Range(1, long.MaxValue, ErrorMessage = "Mã người dùng không hợp lệ.")]
    public long UserId { get; set; }

    [Required(ErrorMessage = "Mã bãi đỗ xe (lotId) là bắt buộc.")]
    [Range(1, int.MaxValue, ErrorMessage = "Mã bãi đỗ xe không hợp lệ.")]
    public int LotId { get; set; }

    /// <summary>
    /// Nhiệm vụ phân công: Bắt buộc chọn GATE, SLOT_VERIFIER hoặc BOTH (không cấp mặc định).
    /// </summary>
    [Required(ErrorMessage = "Nhiệm vụ trực ca (Duty) là bắt buộc. Phải chọn GATE, SLOT_VERIFIER hoặc BOTH.")]
    [RegularExpression("^(GATE|SLOT_VERIFIER|BOTH)$", ErrorMessage = "Nhiệm vụ trực ca phải là GATE, SLOT_VERIFIER hoặc BOTH.")]
    public string Duty { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự.")]
    public string? Notes { get; set; }
}

public class RevokeStaffAssignmentRequestDto
{
    [MaxLength(255, ErrorMessage = "Lý do thu hồi không được vượt quá 255 ký tự.")]
    public string? Reason { get; set; }
}

public class StaffAssignmentFilterDto
{
    public int? LotId { get; set; }
    public long? UserId { get; set; }
    public bool? IsActive { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Số trang phải lớn hơn hoặc bằng 1.")]
    public int Page { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Kích thước trang phải từ 1 đến 100.")]
    public int PageSize { get; set; } = 20;
}

public class AssignedLotSummaryDto
{
    public int LotId { get; set; }
    public string LotName { get; set; } = null!;
    public string? LotAddress { get; set; }
    public bool IsActive { get; set; }
    public DateTime AssignedAt { get; set; }
}
