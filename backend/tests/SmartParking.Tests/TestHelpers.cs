using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Domain.Constants;
using SmartParking.Domain.Entities;
using SmartParking.Infrastructure.Data;

namespace SmartParking.Tests;

public class FakeDbContextTransaction : IDbContextTransaction
{
    public Guid TransactionId { get; } = Guid.NewGuid();
    public void Commit() { }
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Rollback() { }
    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public class TestSmartParkingDbContext : SmartParkingDbContext
{
    public TestSmartParkingDbContext(DbContextOptions<SmartParkingDbContext> options) : base(options) { }

    public override Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IDbContextTransaction>(new FakeDbContextTransaction());
    }
}

public class FakeEmailService : IEmailService
{
    public List<(string ToEmail, string OtpCode)> SentEmails { get; } = new();
    public bool ShouldFail { get; set; } = false;

    public Task<bool> SendVerificationOtpAsync(string toEmail, string otpCode, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(false);
        SentEmails.Add((toEmail, otpCode));
        return Task.FromResult(true);
    }
}

public class FakeRecaptchaService : IRecaptchaService
{
    public bool ReturnValid { get; set; } = true;

    public Task<bool> VerifyAsync(string? token, string? remoteIp = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ReturnValid);
    }
}

public static class TestDbHelper
{
    public static async Task<TestSmartParkingDbContext> CreateInMemoryDbContextAsync()
    {
        var options = new DbContextOptionsBuilder<SmartParkingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new TestSmartParkingDbContext(options);

        // Seed Roles
        context.Roles.AddRange(
            new Role { Id = 1, Code = Roles.Admin, Name = "Quản trị viên" },
            new Role { Id = 2, Code = Roles.Manager, Name = "Quản lý bãi đỗ" },
            new Role { Id = 3, Code = Roles.Staff, Name = "Nhân viên vận hành" },
            new Role { Id = 4, Code = Roles.User, Name = "Người dùng" }
        );

        await context.SaveChangesAsync();
        return context;
    }
}
