using Microsoft.EntityFrameworkCore;
using ParkingOperations.Domain.Entities;

namespace ParkingOperations.Persistence.Data;

public sealed class OperationsDbContext(DbContextOptions<OperationsDbContext> options) : DbContext(options)
{
    public DbSet<CashHandover> CashHandovers => Set<CashHandover>();
    public DbSet<ParkingShift> ParkingShifts => Set<ParkingShift>();
    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();

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
        handover.Property(x => x.OutgoingShiftId).HasColumnName("outgoing_shift_id");
        handover.Property(x => x.IncomingShiftId).HasColumnName("incoming_shift_id");
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
        handover.HasOne<ParkingShift>().WithMany().HasForeignKey(x => x.OutgoingShiftId).OnDelete(DeleteBehavior.Restrict);
        handover.HasOne<ParkingShift>().WithMany().HasForeignKey(x => x.IncomingShiftId).OnDelete(DeleteBehavior.Restrict);
        handover.HasIndex(x => new { x.LotId, x.BusinessDate, x.OutgoingShiftId }).IsUnique()
            .HasDatabaseName("ux_cash_handovers_lot_date_outgoing_shift");
        handover.HasIndex(x => new { x.LotId, x.BusinessDate, x.Status })
            .HasDatabaseName("ix_cash_handovers_lot_date_status");

        var shift = modelBuilder.Entity<ParkingShift>();
        shift.ToTable("shifts");
        shift.HasKey(x => x.Id);
        shift.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        shift.Property(x => x.LotId).HasColumnName("lot_id").IsRequired();
        shift.Property(x => x.BusinessDate).HasColumnName("business_date").IsRequired();
        shift.Property(x => x.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
        shift.Property(x => x.StartsAtUtc).HasColumnName("starts_at_utc").HasColumnType("timestamp with time zone").IsRequired();
        shift.Property(x => x.EndsAtUtc).HasColumnName("ends_at_utc").HasColumnType("timestamp with time zone").IsRequired();
        shift.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        shift.Property(x => x.CreatedByManagerId).HasColumnName("created_by_manager_id").IsRequired();
        shift.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone").IsRequired();
        shift.Property(x => x.StartedAtUtc).HasColumnName("started_at_utc").HasColumnType("timestamp with time zone");
        shift.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc").HasColumnType("timestamp with time zone");
        shift.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        shift.HasIndex(x => new { x.LotId, x.BusinessDate, x.Code }).IsUnique().HasDatabaseName("ux_shifts_lot_business_date_code");
        shift.HasMany(x => x.Assignments).WithOne(x => x.Shift).HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Cascade);

        var assignment = modelBuilder.Entity<ShiftAssignment>();
        assignment.ToTable("shift_assignments");
        assignment.HasKey(x => x.Id);
        assignment.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        assignment.Property(x => x.ShiftId).HasColumnName("shift_id").IsRequired();
        assignment.Property(x => x.StaffId).HasColumnName("staff_id").IsRequired();
        assignment.Property(x => x.DutyCode).HasColumnName("duty_code").HasMaxLength(32).IsRequired();
        assignment.Property(x => x.AssignedByManagerId).HasColumnName("assigned_by_manager_id").IsRequired();
        assignment.Property(x => x.AssignedAtUtc).HasColumnName("assigned_at_utc").HasColumnType("timestamp with time zone").IsRequired();
        assignment.HasIndex(x => new { x.ShiftId, x.StaffId }).IsUnique().HasDatabaseName("ux_shift_assignments_shift_staff");
    }
}
