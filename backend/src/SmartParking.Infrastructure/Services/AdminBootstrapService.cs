using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Common.Options;
using SmartParking.Domain.Constants;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Services;

public class AdminBootstrapService : IAdminBootstrapService
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly AdminBootstrapOptions _options;
    private readonly ILogger<AdminBootstrapService> _logger;

    public AdminBootstrapService(
        IApplicationDbContext context,
        IPasswordHasherService passwordHasher,
        IOptions<AdminBootstrapOptions> options,
        ILogger<AdminBootstrapService> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _options = options.Value;
        _logger = logger;
    }

    public async Task BootstrapAdminAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Admin bootstrap is disabled in configuration (AdminBootstrap:Enabled is false). Skipping admin bootstrap.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.Username) ||
            string.IsNullOrWhiteSpace(_options.Password) ||
            string.IsNullOrWhiteSpace(_options.Email))
        {
            _logger.LogInformation("Admin bootstrap credentials not provided in configuration. Skipping admin bootstrap.");
            return;
        }

        var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Code == Roles.Admin, cancellationToken);
        if (adminRole == null)
        {
            _logger.LogWarning("Admin role not found in database. Ensure roles are seeded before running admin bootstrap.");
            return;
        }

        var adminExists = await _context.Users.AnyAsync(u => u.RoleId == adminRole.Id, cancellationToken);
        if (adminExists)
        {
            _logger.LogInformation("An ADMIN account already exists in database. Admin bootstrap skipped (idempotent).");
            return;
        }

        var normalizedUsername = _options.Username.Trim().ToLowerInvariant();
        var normalizedEmail = _options.Email.Trim().ToLowerInvariant();

        // Check if username or email is already taken by non-admin
        var conflict = await _context.Users.AnyAsync(u => u.Username == normalizedUsername || u.Email == normalizedEmail, cancellationToken);
        if (conflict)
        {
            _logger.LogWarning("Admin bootstrap username '{Username}' or email '{Email}' conflicts with an existing account. Bootstrap aborted.",
                normalizedUsername, normalizedEmail);
            return;
        }

        var adminUser = new User
        {
            Username = normalizedUsername,
            Email = normalizedEmail,
            RoleId = adminRole.Id,
            FullName = string.IsNullOrWhiteSpace(_options.FullName) ? "System Administrator" : _options.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(_options.PhoneNumber) ? null : _options.PhoneNumber.Trim(),
            IsEmailVerified = true,
            IsActive = true,
            ApprovalStatus = ApprovalStatuses.Approved,
            ApprovedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        adminUser.PasswordHash = _passwordHasher.HashPassword(adminUser, _options.Password);

        _context.Users.Add(adminUser);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Initial ADMIN account bootstrapped successfully with username '{Username}'.", normalizedUsername);
    }
}
