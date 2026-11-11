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
    public int? FloorId { get; set; }
    public string? FloorName { get; set; }
    public int? ZoneId { get; set; }
    public string? ZoneName { get; set; }

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

public class CreateStaffAssignmentRequestDto : IValidatableObject
{
    [Required(ErrorMessage = "Mã người dùng (userId) là bắt buộc.")]
    [Range(1, long.MaxValue, ErrorMessage = "Mã người dùng không hợp lệ.")]
    public long UserId { get; set; }

    [Required(ErrorMessage = "Mã bãi đỗ xe (lotId) là bắt buộc.")]
    [Range(1, int.MaxValue, ErrorMessage = "Mã bãi đỗ xe không hợp lệ.")]
    public int LotId { get; set; }

    /// <summary>
    /// Mỗi phân công chỉ có một nhiệm vụ.
    /// </summary>
    [Required(ErrorMessage = "Chọn loại phân công.")]
    [RegularExpression("^(GATE_IN|GATE_OUT|SLOT_VERIFIER)$", ErrorMessage = "Chọn trực cổng vào, trực cổng ra hoặc quản lý khu vực.")]
    public string Duty { get; set; } = string.Empty;

    [Required(ErrorMessage = "Chọn tầng được phân công.")]
    [Range(1, int.MaxValue)]
    public int? FloorId { get; set; }
    [Range(1, int.MaxValue)]
    public int? ZoneId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Duty == StaffDuties.SlotVerifier && ZoneId is not > 0)
            yield return new ValidationResult("Chọn khu vực được phân công.", [nameof(ZoneId)]);
        if (Duty is StaffDuties.GateIn or StaffDuties.GateOut && ZoneId.HasValue)
            yield return new ValidationResult("Phân công trực cổng chỉ chọn tầng, không chọn khu vực.", [nameof(ZoneId)]);
    }

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

public class StaffCandidateFilterDto
{
    [MaxLength(255)]
    public string? Search { get; set; }
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

public class StaffCandidateDto
{
    public long Id { get; set; }
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role => Roles.Staff;
}

public record AssignmentZoneDto(int Id, string Name, string Code);
public record AssignmentFloorDto(int Id, string Name, IReadOnlyList<AssignmentZoneDto> Zones);
public record AssignmentLocationsDto(IReadOnlyList<AssignmentFloorDto> Floors);
