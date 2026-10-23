using System.ComponentModel.DataAnnotations;

namespace SmartParking.Application.DTOs.Vehicles;

public class VehicleDto
{
    public long Id { get; set; }
    public long? UserId { get; set; }
    public string? OwnerFullName { get; set; }
    public string? OwnerUsername { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? Color { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateVehicleRequestDto
{
    [Required(ErrorMessage = "Biển số xe không được để trống.")]
    [StringLength(20, MinimumLength = 3, ErrorMessage = "Biển số xe phải từ 3 đến 20 ký tự.")]
    public string PlateNumber { get; set; } = string.Empty;

    [MaxLength(50, ErrorMessage = "Hãng xe không được vượt quá 50 ký tự.")]
    public string? Brand { get; set; }

    [MaxLength(50, ErrorMessage = "Dòng xe (model) không được vượt quá 50 ký tự.")]
    public string? Model { get; set; }

    [MaxLength(30, ErrorMessage = "Màu sắc không được vượt quá 30 ký tự.")]
    public string? Color { get; set; }

    /// <summary>
    /// ID người dùng sở hữu xe (Chỉ ADMIN được phép gán cho người khác hoặc để null cho xe vãng lai).
    /// Nếu người dùng thông thường gọi, UserId sẽ tự động gán là chính tài khoản của họ.
    /// </summary>
    public long? UserId { get; set; }
}

public class UpdateVehicleRequestDto
{
    [MaxLength(50, ErrorMessage = "Hãng xe không được vượt quá 50 ký tự.")]
    public string? Brand { get; set; }

    [MaxLength(50, ErrorMessage = "Dòng xe không được vượt quá 50 ký tự.")]
    public string? Model { get; set; }

    [MaxLength(30, ErrorMessage = "Màu sắc không được vượt quá 30 ký tự.")]
    public string? Color { get; set; }

    public bool? IsActive { get; set; }
}

public class VehicleFilterDto
{
    public string? SearchPlate { get; set; }
    public long? UserId { get; set; }
    public bool? IsActive { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
