using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartParking.Application.Common.Helpers;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Common.Models;
using SmartParking.Application.DTOs.Vehicles;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Services;

public class VehicleService : IVehicleService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<VehicleService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    public VehicleService(IApplicationDbContext context, ILogger<VehicleService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<VehicleDto>> GetVehiclesAsync(VehicleFilterDto filter, long? currentUserId, bool isAdmin, CancellationToken ct = default)
    {
        var query = _context.Vehicles
            .AsNoTracking()
            .Include(v => v.User)
            .AsQueryable();

        if (!isAdmin)
        {
            if (currentUserId == null)
            {
                return PagedResult<VehicleDto>.Create([], 0, filter.Page, filter.PageSize);
            }
            query = query.Where(v => v.UserId == currentUserId.Value);
        }
        else if (filter.UserId.HasValue)
        {
            query = query.Where(v => v.UserId == filter.UserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchPlate))
        {
            var normalizedSearch = PlateNumberHelper.Normalize(filter.SearchPlate);
            query = query.Where(v => v.PlateNumber.Contains(normalizedSearch));
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(v => v.IsActive == filter.IsActive.Value);
        }

        var totalCount = await query.CountAsync(ct);

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(v => v.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new VehicleDto
            {
                Id = v.Id,
                UserId = v.UserId,
                OwnerFullName = v.User != null ? v.User.FullName : null,
                OwnerUsername = v.User != null ? v.User.Username : null,
                PlateNumber = v.PlateNumber,
                Brand = v.Brand,
                Model = v.Model,
                Color = v.Color,
                IsActive = v.IsActive,
                CreatedAt = v.CreatedAt,
                UpdatedAt = v.UpdatedAt
            })
            .ToListAsync(ct);

        return PagedResult<VehicleDto>.Create(items, totalCount, page, pageSize);
    }

    public async Task<VehicleDto?> GetVehicleByIdAsync(long id, long? currentUserId, bool isAdmin, CancellationToken ct = default)
    {
        var query = _context.Vehicles
            .AsNoTracking()
            .Include(v => v.User)
            .Where(v => v.Id == id);

        if (!isAdmin)
        {
            if (currentUserId == null)
            {
                return null;
            }
            query = query.Where(v => v.UserId == currentUserId.Value);
        }

        var vehicle = await query.FirstOrDefaultAsync(ct);
        if (vehicle == null)
        {
            return null;
        }

        return MapToDto(vehicle);
    }

    public async Task<VehicleDto?> GetVehicleByPlateAsync(string plateNumber, long? currentUserId, bool isAdmin, CancellationToken ct = default)
    {
        var normalizedPlate = PlateNumberHelper.Normalize(plateNumber);
        if (string.IsNullOrWhiteSpace(normalizedPlate))
        {
            return null;
        }

        var query = _context.Vehicles
            .AsNoTracking()
            .Include(v => v.User)
            .Where(v => v.PlateNumber == normalizedPlate);

        if (!isAdmin)
        {
            if (currentUserId == null)
            {
                return null;
            }
            query = query.Where(v => v.UserId == currentUserId.Value);
        }

        var vehicle = await query.FirstOrDefaultAsync(ct);
        if (vehicle == null)
        {
            return null;
        }

        return MapToDto(vehicle);
    }

    public async Task<(bool Success, string Message, VehicleDto? Data)> CreateVehicleAsync(CreateVehicleRequestDto request, long? currentUserId, bool isAdmin, CancellationToken ct = default)
    {
        var normalizedPlate = PlateNumberHelper.Normalize(request.PlateNumber);
        if (!PlateNumberHelper.IsValidPlateFormat(normalizedPlate))
        {
            return (false, "Biển số xe không hợp lệ. Độ dài phải từ 3 đến 20 ký tự.", null);
        }

        long? targetUserId = isAdmin ? request.UserId : currentUserId;

        if (!isAdmin && currentUserId == null)
        {
            return (false, "Không xác định được danh tính người dùng.", null);
        }

        var existingVehicle = await _context.Vehicles
            .Include(v => v.User)
            .FirstOrDefaultAsync(v => v.PlateNumber == normalizedPlate, ct);

        if (existingVehicle != null)
        {
            if (existingVehicle.UserId.HasValue)
            {
                if (existingVehicle.UserId == targetUserId)
                {
                    return (false, "Phương tiện với biển số này đã được đăng ký trong tài khoản của bạn.", null);
                }
                return (false, "Biển số xe này đã được đăng ký bởi một người dùng khác trong hệ thống.", null);
            }

            // Xe vãng lai đã tồn tại (UserId == null)
            // Bảo mật quyền sở hữu: Không tự động công nhận quyền sở hữu cho USER chỉ dựa vào việc biết biển số!
            if (!isAdmin)
            {
                return (false, "Biển số xe này đã được ghi nhận trong hệ thống (xe vãng lai). Tính năng tự động nhận xe tạm khóa để bảo mật quyền sở hữu; vui lòng liên hệ Quản trị viên để xác minh phương tiện.", null);
            }

            // Chỉ ADMIN được phép gán thủ công xe vãng lai cho một User sau khi đã xác minh
            existingVehicle.UserId = targetUserId;
            if (!string.IsNullOrWhiteSpace(request.Brand)) existingVehicle.Brand = request.Brand.Trim();
            if (!string.IsNullOrWhiteSpace(request.Model)) existingVehicle.Model = request.Model.Trim();
            if (!string.IsNullOrWhiteSpace(request.Color)) existingVehicle.Color = request.Color.Trim();
            existingVehicle.IsActive = true;
            existingVehicle.UpdatedAt = DateTime.UtcNow;

            await LogAuditAsync(
                action: "ADMIN_ASSIGN_GUEST_VEHICLE",
                targetEntity: "vehicles",
                targetId: existingVehicle.Id.ToString(),
                oldValues: new { existingVehicle.PlateNumber, UserId = (long?)null },
                newValues: new { existingVehicle.PlateNumber, existingVehicle.UserId, existingVehicle.Brand, existingVehicle.Model },
                actorId: currentUserId,
                ct: ct
            );

            await _context.SaveChangesAsync(ct);
            return (true, "Quản trị viên liên kết phương tiện vào tài khoản thành công.", MapToDto(existingVehicle));
        }

        await using var transaction = await _context.BeginTransactionAsync(ct);
        try
        {
            var vehicle = new Vehicle
            {
                PlateNumber = normalizedPlate,
                UserId = targetUserId,
                Brand = request.Brand?.Trim(),
                Model = request.Model?.Trim(),
                Color = request.Color?.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Vehicles.Add(vehicle);
            await _context.SaveChangesAsync(ct);

            await LogAuditAsync(
                action: "CREATE_VEHICLE",
                targetEntity: "vehicles",
                targetId: vehicle.Id.ToString(),
                oldValues: null,
                newValues: new { vehicle.PlateNumber, vehicle.UserId, vehicle.Brand, vehicle.Model, vehicle.Color },
                actorId: currentUserId,
                ct: ct
            );

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return (true, "Đăng ký phương tiện thành công.", MapToDto(vehicle));
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(ct);
            if (IsPostgresUniqueViolation(ex, "uq_vehicles_plate_number"))
            {
                return (false, "Biển số xe này đã được đăng ký trong hệ thống.", null);
            }

            _logger.LogError(ex, "Lỗi cơ sở dữ liệu khi đăng ký xe biển số {PlateNumber}", normalizedPlate);
            throw;
        }
    }

    private static bool IsPostgresUniqueViolation(DbUpdateException ex, string constraintName)
    {
        if (ex.InnerException is Npgsql.PostgresException pgEx)
        {
            return pgEx.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation &&
                   (string.IsNullOrEmpty(constraintName) || pgEx.ConstraintName?.Equals(constraintName, StringComparison.OrdinalIgnoreCase) == true);
        }

        var innerMsg = ex.InnerException?.Message ?? ex.Message;
        return innerMsg.Contains("23505") || innerMsg.Contains(constraintName, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<(bool Success, string Message, VehicleDto? Data)> UpdateVehicleAsync(long id, UpdateVehicleRequestDto request, long? currentUserId, bool isAdmin, CancellationToken ct = default)
    {
        var vehicle = await _context.Vehicles
            .Include(v => v.User)
            .FirstOrDefaultAsync(v => v.Id == id, ct);

        if (vehicle == null)
        {
            return (false, $"Không tìm thấy phương tiện với ID: {id}.", null);
        }

        if (!isAdmin && vehicle.UserId != currentUserId)
        {
            return (false, "Bạn không có quyền chỉnh sửa phương tiện của người khác.", null);
        }

        var oldValues = new
        {
            vehicle.Brand,
            vehicle.Model,
            vehicle.Color,
            vehicle.IsActive
        };

        if (request.Brand != null) vehicle.Brand = request.Brand.Trim();
        if (request.Model != null) vehicle.Model = request.Model.Trim();
        if (request.Color != null) vehicle.Color = request.Color.Trim();
        if (request.IsActive.HasValue) vehicle.IsActive = request.IsActive.Value;
        vehicle.UpdatedAt = DateTime.UtcNow;

        var newValues = new
        {
            vehicle.Brand,
            vehicle.Model,
            vehicle.Color,
            vehicle.IsActive
        };

        await LogAuditAsync(
            action: "UPDATE_VEHICLE",
            targetEntity: "vehicles",
            targetId: vehicle.Id.ToString(),
            oldValues: oldValues,
            newValues: newValues,
            actorId: currentUserId,
            ct: ct
        );

        await _context.SaveChangesAsync(ct);

        return (true, "Cập nhật thông tin phương tiện thành công.", MapToDto(vehicle));
    }

    public async Task<(bool Success, string Message)> DeleteVehicleAsync(long id, long? currentUserId, bool isAdmin, CancellationToken ct = default)
    {
        var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (vehicle == null)
        {
            return (false, $"Không tìm thấy phương tiện với ID: {id}.");
        }

        if (!isAdmin && vehicle.UserId != currentUserId)
        {
            return (false, "Bạn không có quyền xóa phương tiện của người khác.");
        }

        var hasSessions = await _context.ParkingSessions.AnyAsync(s => s.VehicleId == id, ct);
        if (hasSessions)
        {
            return (false, "Phương tiện đã có lịch sử gửi xe, không thể xóa khỏi hệ thống. Vui lòng cập nhật trạng thái ngừng hoạt động (IsActive = false).");
        }

        _context.Vehicles.Remove(vehicle);

        await LogAuditAsync(
            action: "DELETE_VEHICLE",
            targetEntity: "vehicles",
            targetId: id.ToString(),
            oldValues: new { vehicle.PlateNumber, vehicle.UserId },
            newValues: null,
            actorId: currentUserId,
            ct: ct
        );

        await _context.SaveChangesAsync(ct);
        return (true, "Xóa phương tiện thành công.");
    }

    private static VehicleDto MapToDto(Vehicle vehicle)
    {
        return new VehicleDto
        {
            Id = vehicle.Id,
            UserId = vehicle.UserId,
            OwnerFullName = vehicle.User?.FullName,
            OwnerUsername = vehicle.User?.Username,
            PlateNumber = vehicle.PlateNumber,
            Brand = vehicle.Brand,
            Model = vehicle.Model,
            Color = vehicle.Color,
            IsActive = vehicle.IsActive,
            CreatedAt = vehicle.CreatedAt,
            UpdatedAt = vehicle.UpdatedAt
        };
    }

    private async Task LogAuditAsync(string action, string targetEntity, string targetId, object? oldValues, object? newValues, long? actorId, CancellationToken ct)
    {
        var audit = new AuditLog
        {
            Action = action,
            TargetEntity = targetEntity,
            TargetId = targetId,
            ActorId = actorId,
            OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues, JsonOptions) : null,
            NewValues = newValues != null ? JsonSerializer.Serialize(newValues, JsonOptions) : null,
            CreatedAt = DateTime.UtcNow
        };

        _context.AuditLogs.Add(audit);
    }
}
