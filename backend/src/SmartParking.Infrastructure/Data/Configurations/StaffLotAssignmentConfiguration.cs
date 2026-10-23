using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Data.Configurations;

public class StaffLotAssignmentConfiguration : IEntityTypeConfiguration<StaffLotAssignment>
{
    public void Configure(EntityTypeBuilder<StaffLotAssignment> builder)
    {
        builder.ToTable("staff_lot_assignments");

        builder.HasKey(a => a.Id)
            .HasName("pk_staff_lot_assignments");

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

        builder.Property(a => a.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.HasOne(a => a.User)
            .WithMany(u => u.StaffLotAssignments)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_staff_lot_assignments_user");

        builder.Property(a => a.LotId)
            .HasColumnName("lot_id")
            .IsRequired();

        builder.HasOne(a => a.Lot)
            .WithMany(l => l.StaffAssignments)
            .HasForeignKey(a => a.LotId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_staff_lot_assignments_lot");

        builder.Property(a => a.AssignedBy)
            .HasColumnName("assigned_by")
            .IsRequired();

        builder.HasOne(a => a.AssignedByUser)
            .WithMany(u => u.AssignedStaffAssignments)
            .HasForeignKey(a => a.AssignedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_staff_lot_assignments_assigned_by");

        builder.Property(a => a.AssignedAt)
            .HasColumnName("assigned_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.Property(a => a.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(a => a.RevokedBy)
            .HasColumnName("revoked_by");

        builder.HasOne(a => a.RevokedByUser)
            .WithMany(u => u.RevokedStaffAssignments)
            .HasForeignKey(a => a.RevokedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_staff_lot_assignments_revoked_by");

        builder.Property(a => a.RevokedAt)
            .HasColumnName("revoked_at");

        builder.Property(a => a.RevocationReason)
            .HasColumnName("revocation_reason")
            .HasMaxLength(255);

        builder.Property(a => a.Notes)
            .HasColumnName("notes");

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.Property(a => a.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        // 1. Partial unique index to prevent duplicate active assignments for the same staff member and lot
        builder.HasIndex(a => new { a.UserId, a.LotId })
            .IsUnique()
            .HasFilter("is_active = TRUE")
            .HasDatabaseName("uq_staff_lot_assignments_active");

        // 2. Index for rapid real-time authorization checks by user
        builder.HasIndex(a => a.UserId)
            .HasFilter("is_active = TRUE")
            .HasDatabaseName("ix_staff_lot_assignments_user_active");

        // 3. Index for querying staff members assigned to a specific lot
        builder.HasIndex(a => a.LotId)
            .HasFilter("is_active = TRUE")
            .HasDatabaseName("ix_staff_lot_assignments_lot_active");
    }
}
