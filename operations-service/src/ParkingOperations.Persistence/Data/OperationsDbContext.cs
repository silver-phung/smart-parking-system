using Microsoft.EntityFrameworkCore;
using ParkingOperations.Domain.Entities;

namespace ParkingOperations.Persistence.Data;

public sealed class OperationsDbContext(DbContextOptions<OperationsDbContext> options) : DbContext(options)
{
    public DbSet<CashHandover> CashHandovers => Set<CashHandover>();
    public DbSet<ParkingShift> ParkingShifts => Set<ParkingShift>();
    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();
    public DbSet<CashReconciliation> CashReconciliations => Set<CashReconciliation>();

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

        var reconciliation = modelBuilder.Entity<CashReconciliation>();
        reconciliation.ToTable("cash_reconciliations");
        reconciliation.HasKey(x => x.Id);
        reconciliation.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        reconciliation.Property(x => x.ShiftId).HasColumnName("shift_id").IsRequired();
        reconciliation.Property(x => x.OpeningCash).HasColumnName("opening_cash").HasPrecision(14, 2);
        reconciliation.Property(x => x.CashReceipts).HasColumnName("cash_receipts").HasPrecision(14, 2);
        reconciliation.Property(x => x.CashRefunds).HasColumnName("cash_refunds").HasPrecision(14, 2);
        reconciliation.Property(x => x.CashDisbursements).HasColumnName("cash_disbursements").HasPrecision(14, 2);
        reconciliation.Property(x => x.ExpectedClosingCash).HasColumnName("expected_closing_cash").HasPrecision(14, 2);
        reconciliation.Property(x => x.CountedClosingCash).HasColumnName("counted_closing_cash").HasPrecision(14, 2);
        reconciliation.Property(x => x.Variance).HasColumnName("variance").HasPrecision(14, 2);
        reconciliation.Property(x => x.PaymentSummaryReference).HasColumnName("payment_summary_reference").HasMaxLength(200).IsRequired();
        reconciliation.Property(x => x.PaymentSummaryCapturedAtUtc).HasColumnName("payment_summary_captured_at_utc").HasColumnType("timestamp with time zone").IsRequired();
        reconciliation.Property(x => x.VarianceReason).HasColumnName("variance_reason").HasMaxLength(500);
        reconciliation.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        reconciliation.Property(x => x.SubmittedByManagerId).HasColumnName("submitted_by_manager_id").IsRequired();
        reconciliation.Property(x => x.SubmittedAtUtc).HasColumnName("submitted_at_utc").HasColumnType("timestamp with time zone").IsRequired();
        reconciliation.Property(x => x.ApprovedByManagerId).HasColumnName("approved_by_manager_id");
        reconciliation.Property(x => x.ApprovedAtUtc).HasColumnName("approved_at_utc").HasColumnType("timestamp with time zone");
        reconciliation.Property(x => x.ApprovalNote).HasColumnName("approval_note").HasMaxLength(500);
        reconciliation.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        reconciliation.HasOne(x => x.Shift).WithOne().HasForeignKey<CashReconciliation>(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);
        reconciliation.HasIndex(x => x.ShiftId).IsUnique().HasDatabaseName("ux_cash_reconciliations_shift");
        reconciliation.HasIndex(x => new { x.Status, x.SubmittedAtUtc }).HasDatabaseName("ix_cash_reconciliations_status_submitted_at");
    }
}
