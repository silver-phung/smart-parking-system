using Microsoft.EntityFrameworkCore;
using ParkingOperations.Domain.Entities;

namespace ParkingOperations.Persistence.Data;

public sealed class OperationsDbContext(DbContextOptions<OperationsDbContext> options) : DbContext(options)
{
    public DbSet<CashHandover> CashHandovers => Set<CashHandover>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var handover = modelBuilder.Entity<CashHandover>();
        handover.ToTable("cash_handovers");
        handover.HasKey(x => x.Id);
        handover.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        handover.Property(x => x.LotId).HasColumnName("lot_id").IsRequired();
        handover.Property(x => x.BusinessDate).HasColumnName("business_date").IsRequired();
        handover.Property(x => x.FromShiftCode).HasColumnName("from_shift_code").HasMaxLength(20).IsRequired();
        handover.Property(x => x.ToShiftCode).HasColumnName("to_shift_code").HasMaxLength(20).IsRequired();
        handover.Property(x => x.OutgoingStaffId).HasColumnName("outgoing_staff_id").IsRequired();
        handover.Property(x => x.IncomingStaffId).HasColumnName("incoming_staff_id").IsRequired();
        handover.Property(x => x.CreatedByManagerId).HasColumnName("created_by_manager_id").IsRequired();
        handover.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone").IsRequired();
        handover.Property(x => x.OutgoingCountedAmount).HasColumnName("outgoing_counted_amount").HasPrecision(14, 2);
        handover.Property(x => x.OutgoingConfirmedBy).HasColumnName("outgoing_confirmed_by");
        handover.Property(x => x.OutgoingConfirmedAtUtc).HasColumnName("outgoing_confirmed_at_utc").HasColumnType("timestamp with time zone");
        handover.Property(x => x.IncomingCountedAmount).HasColumnName("incoming_counted_amount").HasPrecision(14, 2);
        handover.Property(x => x.IncomingConfirmedBy).HasColumnName("incoming_confirmed_by");
        handover.Property(x => x.IncomingConfirmedAtUtc).HasColumnName("incoming_confirmed_at_utc").HasColumnType("timestamp with time zone");
        handover.Property(x => x.DisputeReason).HasColumnName("dispute_reason").HasMaxLength(500);
        handover.Property(x => x.DisputedBy).HasColumnName("disputed_by");
        handover.Property(x => x.DisputedAtUtc).HasColumnName("disputed_at_utc").HasColumnType("timestamp with time zone");
        handover.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        handover.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        handover.HasIndex(x => new { x.LotId, x.BusinessDate, x.FromShiftCode }).IsUnique()
            .HasDatabaseName("ux_cash_handovers_lot_date_from_shift");
        handover.HasIndex(x => new { x.LotId, x.BusinessDate, x.Status })
            .HasDatabaseName("ix_cash_handovers_lot_date_status");
    }
}
