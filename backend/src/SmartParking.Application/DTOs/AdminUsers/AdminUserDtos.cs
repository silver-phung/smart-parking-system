using System.ComponentModel.DataAnnotations;
using SmartParking.Application.DTOs.StaffAssignments;

namespace SmartParking.Application.DTOs.AdminUsers;

public class AdminUserFilterDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Search { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
    public string? ApprovalStatus { get; set; }
}

public class AdminUserSummaryDto
{
    public long Id { get; set; }
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public string Role { get; set; } = null!;
    public bool IsActive { get; set; }
    public bool IsEmailVerified { get; set; }
    public string ApprovalStatus { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class AdminUserDetailDto : AdminUserSummaryDto
{
    public string? AvatarUrl { get; set; }
    public long? ApprovedBy { get; set; }
    public string? ApprovedByUsername { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockoutEndAt { get; set; }
    public int ActiveAssignmentsCount { get; set; }
    public int VehiclesCount { get; set; }
    public List<AssignedLotSummaryDto> ActiveAssignments { get; set; } = [];
}

public class AdminCreateUserRequestDto
{
    [Required(ErrorMessage = "Tên đăng nhập không được để trống")]
    [RegularExpression(@"^[a-zA-Z0-9_.]{3,50}$", ErrorMessage = "Tên đăng nhập chỉ gồm 3-50 ký tự chữ cái, số, dấu chấm hoặc gạch dưới")]
    public string Username { get; set; } = null!;

    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress(ErrorMessage = "Địa chỉ email không đúng định dạng")]
    [MaxLength(255)]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Mật khẩu không được để trống")]
    [MinLength(8, ErrorMessage = "Mật khẩu phải có tối thiểu 8 ký tự")]
    [MaxLength(100)]
    public string Password { get; set; } = null!;

    [Required(ErrorMessage = "Họ và tên không được để trống")]
    [MaxLength(100)]
    public string FullName { get; set; } = null!;

    [MaxLength(20)]
    [Phone(ErrorMessage = "Số điện thoại không đúng định dạng")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Vai trò không được để trống")]
    public string Role { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}

public class AdminUpdateStatusRequestDto
{
    [Required(ErrorMessage = "Trạng thái hoạt động không được để trống")]
    public bool IsActive { get; set; }

    [MaxLength(500, ErrorMessage = "Lý do không được vượt quá 500 ký tự")]
    public string? Reason { get; set; }
}

public class AdminUpdateRoleRequestDto
{
    [Required(ErrorMessage = "Vai trò không được để trống")]
    public string Role { get; set; } = null!;

    [MaxLength(500, ErrorMessage = "Lý do không được vượt quá 500 ký tự")]
    public string? Reason { get; set; }
}
