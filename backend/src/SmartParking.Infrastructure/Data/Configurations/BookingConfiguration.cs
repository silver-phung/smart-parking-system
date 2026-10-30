using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Data.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("bookings", t =>
        {
            t.HasCheckConstraint("chk_bookings_status", "status IN ('PENDING', 'CHECKED_IN', 'CANCELLED', 'EXPIRED')");
            t.HasCheckConstraint("chk_bookings_time_order", "end_time IS NULL OR end_time >= start_time");
            t.HasCheckConstraint("chk_bookings_plate_normalized", "plate_number = UPPER(TRIM(plate_number))");
        });

        builder.HasKey(b => b.Id)
            .HasName("pk_bookings");

        builder.Property(b => b.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

        builder.Property(b => b.LotId)
            .HasColumnName("lot_id")
            .IsRequired();

        builder.HasOne(b => b.Lot)
            .WithMany(l => l.Bookings)
            .HasForeignKey(b => b.LotId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_bookings_lot");

        builder.Property(b => b.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.HasOne(b => b.User)
            .WithMany(u => u.Bookings)
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_bookings_user");

        builder.Property(b => b.VehicleId)
            .HasColumnName("vehicle_id")
            .IsRequired();

        builder.HasOne(b => b.Vehicle)
            .WithMany(v => v.Bookings)
            .HasForeignKey(b => b.VehicleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_bookings_vehicle");

        builder.Property(b => b.PlateNumber)
            .HasColumnName("plate_number")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(b => b.StartTime)
            .HasColumnName("start_time")
            .IsRequired();

        builder.Property(b => b.EndTime)
            .HasColumnName("end_time");

        builder.Property(b => b.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasDefaultValue("PENDING")
            .IsRequired();

        builder.Property(b => b.CheckedInAt)
            .HasColumnName("checked_in_at");

        builder.Property(b => b.CancelledAt)
            .HasColumnName("cancelled_at");

        builder.Property(b => b.CancellationReason)
            .HasColumnName("cancellation_reason")
            .HasMaxLength(500);

        builder.Property(b => b.Notes)
            .HasColumnName("notes")
            .HasMaxLength(500);

        builder.Property(b => b.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(b => b.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Indexes for performance and invariant integrity
        builder.HasIndex(b => b.UserId)
            .HasDatabaseName("ix_bookings_user_id");

        builder.HasIndex(b => new { b.LotId, b.Status })
            .HasDatabaseName("ix_bookings_lot_status");

        builder.HasIndex(b => new { b.PlateNumber, b.Status })
            .HasDatabaseName("ix_bookings_plate_status");

        // Unique index: No vehicle can have more than one PENDING booking at any time
        builder.HasIndex(b => b.VehicleId)
            .IsUnique()
            .HasFilter("status = 'PENDING'")
            .HasDatabaseName("uq_bookings_pending_vehicle");
    }
}
