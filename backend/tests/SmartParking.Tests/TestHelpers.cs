using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
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

public class CustomWebApplicationFactory : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove provider callbacks too, otherwise Npgsql options leak into the in-memory context.
            var descriptors = services.Where(d =>
                d.ServiceType == typeof(Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<SmartParkingDbContext>) ||
                d.ServiceType == typeof(Microsoft.EntityFrameworkCore.DbContextOptions<SmartParkingDbContext>) ||
                d.ServiceType == typeof(Microsoft.EntityFrameworkCore.DbContextOptions) ||
                d.ServiceType == typeof(SmartParkingDbContext) ||
                d.ServiceType == typeof(IApplicationDbContext)).ToList();

            foreach (var d in descriptors)
            {
                services.Remove(d);
            }

            var inMemoryServiceProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<SmartParkingDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
                options.UseInternalServiceProvider(inMemoryServiceProvider);
                options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
            });

            services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<SmartParkingDbContext>());

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SmartParkingDbContext>();

            // Seed Roles
            db.Roles.AddRange(
                new Role { Id = 1, Code = Roles.Admin, Name = "Quản trị viên" },
                new Role { Id = 2, Code = Roles.Manager, Name = "Quản lý bãi đỗ" },
                new Role { Id = 3, Code = Roles.Staff, Name = "Nhân viên vận hành" },
                new Role { Id = 4, Code = Roles.User, Name = "Người dùng" }
            );

            // Seed Base Test Users
            db.Users.AddRange(
                new User { Id = 9001, Username = "test_admin", Email = "admin@smartparking.test", FullName = "Test Admin", PasswordHash = "dummy", RoleId = 1, IsActive = true, ApprovalStatus = "APPROVED" },
                new User { Id = 9002, Username = "test_manager", Email = "manager@smartparking.test", FullName = "Test Manager", PasswordHash = "dummy", RoleId = 2, IsActive = true, ApprovalStatus = "APPROVED" },
                new User { Id = 9003, Username = "test_staff", Email = "staff@smartparking.test", FullName = "Test Staff", PasswordHash = "dummy", RoleId = 3, IsActive = true, ApprovalStatus = "APPROVED" },
                new User { Id = 9004, Username = "test_user", Email = "user@smartparking.test", FullName = "Test User", PasswordHash = "dummy", RoleId = 4, IsActive = true, ApprovalStatus = "APPROVED" },
                new User { Id = 9999, Username = "test_fallback", Email = "fallback@smartparking.test", FullName = "Test Fallback", PasswordHash = "dummy", RoleId = 1, IsActive = true, ApprovalStatus = "APPROVED" }
            );

            // Seed Lot 1
            var lot = new ParkingLot
            {
                Id = 1,
                Name = "Bãi Trung Tâm Test",
                Address = "123 Test",
                TotalCapacity = 50,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.ParkingLots.Add(lot);

            // Seed Floor 1
            var floor = new ParkingFloor
            {
                Id = 1,
                LotId = 1,
                FloorNumber = 1,
                Name = "Tầng 1",
                TotalSlots = 10,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.ParkingFloors.Add(floor);

            // Seed Slot 1
            var slot = new ParkingSlot
            {
                Id = 1,
                FloorId = 1,
                SlotCode = "A-01",
                PhysicalStatus = SlotPhysicalStatuses.Empty,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.ParkingSlots.Add(slot);

            // Seed Slot 2
            var slot2 = new ParkingSlot
            {
                Id = 2,
                FloorId = 1,
                SlotCode = "A-02",
                PhysicalStatus = SlotPhysicalStatuses.Empty,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.ParkingSlots.Add(slot2);

            // Seed Vehicle 1
            var vehicle = new Vehicle
            {
                Id = 1,
                PlateNumber = "51A-12345",
                UserId = 100,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Vehicles.Add(vehicle);

            // Seed Session 1
            var session = new ParkingSession
            {
                Id = 1,
                LotId = 1,
                VehicleId = 1,
                PlateNumber = "51A-12345",
                CurrentSlotId = 1,
                Status = SessionStatuses.Active,
                CheckInTime = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.ParkingSessions.Add(session);

            // Seed Dispatch 1
            var dispatch = new ParkingDispatch
            {
                Id = 1,
                SessionId = 1,
                AssignedSlotId = 1,
                Status = DispatchStatuses.Assigned,
                DispatchMode = DispatchModes.Manual,
                DispatchedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.ParkingDispatches.Add(dispatch);

            db.SaveChanges();
        });
    }
}
