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
