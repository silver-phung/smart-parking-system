using Microsoft.AspNetCore.Identity;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Services;

public class PasswordHasherService : IPasswordHasherService
{
    private readonly PasswordHasher<User> _hasher = new();
    private static readonly User DummyUser = new() { Id = 0, Username = "dummy_timing_mitigation" };

    // Dynamically generated valid ASP.NET Identity v3 PBKDF2 hash to ensure 100,000 real iterations
    private static readonly Lazy<string> ValidDummyHash = new(() =>
    {
        var hasher = new PasswordHasher<User>();
        return hasher.HashPassword(DummyUser, "DummyTimingAttackMitigationPassword@2026!");
    });

    public string HashPassword(User user, string password)
    {
        return _hasher.HashPassword(user, password);
    }

    public bool VerifyPassword(User user, string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrWhiteSpace(hashedPassword) || string.IsNullOrWhiteSpace(providedPassword))
        {
            return false;
        }

        var result = _hasher.VerifyHashedPassword(user, hashedPassword, providedPassword);
        return result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded;
    }

    public void DummyVerify()
    {
        // Executes the full 100,000 PBKDF2 iterations to ensure constant-time execution
        _hasher.VerifyHashedPassword(DummyUser, ValidDummyHash.Value, "WrongDummyPasswordProvided123!");
    }
}
