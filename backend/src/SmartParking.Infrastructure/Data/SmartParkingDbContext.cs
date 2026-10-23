using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Data;

public class SmartParkingDbContext : DbContext, IApplicationDbContext
{
    public SmartParkingDbContext(DbContextOptions<SmartParkingDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserToken> UserTokens => Set<UserToken>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ParkingLot> ParkingLots => Set<ParkingLot>();
    public DbSet<ParkingFloor> ParkingFloors => Set<ParkingFloor>();
    public DbSet<ParkingSlot> ParkingSlots => Set<ParkingSlot>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<ParkingSession> ParkingSessions => Set<ParkingSession>();
    public DbSet<ParkingDispatch> ParkingDispatches => Set<ParkingDispatch>();
    public DbSet<StaffLotAssignment> StaffLotAssignments => Set<StaffLotAssignment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public virtual Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return Database.BeginTransactionAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SmartParkingDbContext).Assembly);
    }
}
