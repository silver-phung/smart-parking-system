using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Data.Configurations;

public class ParkingSessionConfiguration : IEntityTypeConfiguration<ParkingSession>
{
    public void Configure(EntityTypeBuilder<ParkingSession> builder)
    {
        builder.ToTable("parking_sessions", t =>
        {
            t.HasCheckConstraint("chk_parking_sessions_status", "status IN ('ACTIVE', 'PAID', 'COMPLETED', 'CANCELLED')");
            t.HasCheckConstraint("chk_parking_sessions_fee", "total_fee >= 0");
            t.HasCheckConstraint("chk_parking_sessions_checkout_time", "check_out_time IS NULL OR check_out_time >= check_in_time");
            t.HasCheckConstraint("chk_parking_sessions_plate_normalized", "plate_number = UPPER(TRIM(plate_number))");
        });

        builder.HasKey(s => s.Id)
            .HasName("pk_parking_sessions");

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

        builder.Property(s => s.LotId)
            .HasColumnName("lot_id")
            .IsRequired();

        builder.HasOne(s => s.Lot)
            .WithMany(l => l.Sessions)
            .HasForeignKey(s => s.LotId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_parking_sessions_lot");

        builder.Property(s => s.VehicleId)
            .HasColumnName("vehicle_id");

        builder.HasIndex(s => s.VehicleId)
            .HasDatabaseName("ix_parking_sessions_vehicle_id");

        builder.HasOne(s => s.Vehicle)
            .WithMany(v => v.Sessions)
            .HasForeignKey(s => s.VehicleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_parking_sessions_vehicle");

        builder.Property(s => s.PlateNumber)
            .HasColumnName("plate_number")
            .HasMaxLength(20)
            .IsRequired();

        // Anti-Passback: No duplicate active/paid sessions for the same vehicle plate
        builder.HasIndex(s => s.PlateNumber)
            .IsUnique()
            .HasFilter("status IN ('ACTIVE', 'PAID')")
            .HasDatabaseName("uq_parking_sessions_active_plate");

        builder.Property(s => s.CheckInTime)
            .HasColumnName("check_in_time")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(s => s.CheckOutTime)
            .HasColumnName("check_out_time");

        builder.Property(s => s.CurrentSlotId)
            .HasColumnName("current_slot_id");

        builder.HasIndex(s => s.CurrentSlotId)
            .HasDatabaseName("ix_parking_sessions_current_slot_id");

        builder.HasOne(s => s.CurrentSlot)
            .WithMany(sl => sl.CurrentSessions)
            .HasForeignKey(s => s.CurrentSlotId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_parking_sessions_slot");

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasDefaultValue("ACTIVE")
            .IsRequired();

        builder.HasIndex(s => new { s.LotId, s.Status })
            .HasDatabaseName("ix_parking_sessions_lot_status");

        builder.HasIndex(s => s.CheckInTime)
            .HasDatabaseName("ix_parking_sessions_check_in_time");

        builder.Property(s => s.TotalFee)
            .HasColumnName("total_fee")
            .HasPrecision(12, 2)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(s => s.PaidAt)
            .HasColumnName("paid_at");

        builder.Property(s => s.CheckInImageUrl)
            .HasColumnName("check_in_image_url")
            .HasMaxLength(500);

        builder.Property(s => s.CheckOutImageUrl)
            .HasColumnName("check_out_image_url")
            .HasMaxLength(500);

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();
    }
}
