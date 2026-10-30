using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Data.Configurations;

public class ParkingDispatchConfiguration : IEntityTypeConfiguration<ParkingDispatch>
{
    public void Configure(EntityTypeBuilder<ParkingDispatch> builder)
    {
        builder.ToTable("parking_dispatches", t =>
        {
            t.HasCheckConstraint("chk_parking_dispatches_status", "status IN ('PENDING', 'ASSIGNED', 'OCCUPIED', 'WRONG_SLOT', 'CANCELLED')");
            t.HasCheckConstraint("chk_parking_dispatches_mode", "dispatch_mode IN ('AUTO', 'MANUAL')");
        });

        builder.HasKey(d => d.Id)
            .HasName("pk_parking_dispatches");

        builder.Property(d => d.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

        builder.Property(d => d.SessionId)
            .HasColumnName("session_id")
            .IsRequired();

        builder.HasOne(d => d.Session)
            .WithMany(s => s.Dispatches)
            .HasForeignKey(d => d.SessionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_parking_dispatches_session");

        builder.Property(d => d.AssignedSlotId)
            .HasColumnName("assigned_slot_id")
            .IsRequired();

        builder.HasOne(d => d.AssignedSlot)
            .WithMany(sl => sl.AssignedDispatches)
            .HasForeignKey(d => d.AssignedSlotId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_parking_dispatches_assigned_slot");

        builder.Property(d => d.ActualSlotId)
            .HasColumnName("actual_slot_id");

        builder.HasIndex(d => d.ActualSlotId)
            .HasDatabaseName("ix_parking_dispatches_actual_slot_id");

        builder.HasOne(d => d.ActualSlot)
            .WithMany(sl => sl.ActualDispatches)
            .HasForeignKey(d => d.ActualSlotId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_parking_dispatches_actual_slot");

        // Anti-Collision: A slot cannot be assigned to more than one active dispatch simultaneously
        builder.HasIndex(d => d.AssignedSlotId)
            .IsUnique()
            .HasFilter("status = 'ASSIGNED'")
            .HasDatabaseName("uq_parking_dispatches_assigned_slot");

        // Single active dispatch per session
        builder.HasIndex(d => d.SessionId)
            .IsUnique()
            .HasFilter("status IN ('PENDING', 'ASSIGNED')")
            .HasDatabaseName("uq_parking_dispatches_active_session");

        builder.Property(d => d.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasDefaultValue("PENDING")
            .IsRequired();

        builder.Property(d => d.DispatchMode)
            .HasColumnName("dispatch_mode")
            .HasMaxLength(20)
            .HasDefaultValue("AUTO")
            .IsRequired();

        builder.Property(d => d.DispatchedAt)
            .HasColumnName("dispatched_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(d => d.ConfirmedAt)
            .HasColumnName("confirmed_at");

        builder.Property(d => d.Notes)
            .HasColumnName("notes")
            .HasMaxLength(255);

        builder.Property(d => d.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(d => d.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();
    }
}
