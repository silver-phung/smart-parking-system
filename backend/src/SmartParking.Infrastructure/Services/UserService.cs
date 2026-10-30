using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.DTOs.Auth;

namespace SmartParking.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<UserService> _logger;

    public UserService(IApplicationDbContext _context, ILogger<UserService> logger)
    {
        this._context = _context;
        _logger = logger;
    }

    public async Task<UserProfileDto?> GetUserProfileAsync(long userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("UserProfile request for non-existent userId: {UserId}", userId);
            return null;
        }

        return new UserProfileDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role.Code,
            IsEmailVerified = user.IsEmailVerified,
            ApprovalStatus = user.ApprovalStatus,
            CreatedAt = user.CreatedAt
        };
    }

    public async Task<UserProfileDto?> UpdateUserProfileAsync(long userId, UpdateProfileRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("Profile update attempted for non-existent userId: {UserId}", userId);
            return null;
        }

        // Only allow updating FullName and PhoneNumber
        user.FullName = request.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Profile updated successfully for userId: {UserId}", userId);

        return new UserProfileDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role.Code,
            IsEmailVerified = user.IsEmailVerified,
            ApprovalStatus = user.ApprovalStatus,
            CreatedAt = user.CreatedAt
        };
    }
}
