using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using SmartParking.Domain.Entities;

namespace SmartParking.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserToken> UserTokens { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<ParkingLot> ParkingLots { get; }
    DbSet<ParkingFloor> ParkingFloors { get; }
    DbSet<ParkingSlot> ParkingSlots { get; }
    DbSet<Vehicle> Vehicles { get; }
    DbSet<ParkingSession> ParkingSessions { get; }
    DbSet<ParkingDispatch> ParkingDispatches { get; }
    DbSet<StaffLotAssignment> StaffLotAssignments { get; }
    DbSet<AuditLog> AuditLogs { get; }

    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
