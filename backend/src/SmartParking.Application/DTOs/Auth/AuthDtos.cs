using System.ComponentModel.DataAnnotations;

namespace SmartParking.Application.DTOs.Auth;

public class RegisterRequestDto
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

    public string? RecaptchaToken { get; set; }
}

public class VerifyEmailRequestDto
{
    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress(ErrorMessage = "Địa chỉ email không đúng định dạng")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Mã xác minh không được để trống")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã xác minh phải là chuỗi 6 chữ số")]
    public string Code { get; set; } = null!;
}

public class ResendVerificationRequestDto
{
    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress(ErrorMessage = "Địa chỉ email không đúng định dạng")]
    public string Email { get; set; } = null!;
}

public class LoginRequestDto
{
    [Required(ErrorMessage = "Tên đăng nhập không được để trống")]
    public string Username { get; set; } = null!;

    [Required(ErrorMessage = "Mật khẩu không được để trống")]
    public string Password { get; set; } = null!;
}

public class RefreshTokenRequestDto
{
    [Required(ErrorMessage = "Refresh Token không được để trống")]
    public string RefreshToken { get; set; } = null!;
}

public class LogoutRequestDto
{
    [Required(ErrorMessage = "Refresh Token không được để trống")]
    public string RefreshToken { get; set; } = null!;
}

public class AuthResponseDto
{
    public string AccessToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; } = 900;
    public UserProfileDto UserProfile { get; set; } = null!;
}

public class UserProfileDto
{
    public long Id { get; set; }
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = null!;
    public bool IsEmailVerified { get; set; }
    public string ApprovalStatus { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

public class UpdateProfileRequestDto
{
    [Required(ErrorMessage = "Họ và tên không được để trống")]
    [MaxLength(100)]
    public string FullName { get; set; } = null!;

    [MaxLength(20)]
    [Phone(ErrorMessage = "Số điện thoại không đúng định dạng")]
    public string? PhoneNumber { get; set; }
}
