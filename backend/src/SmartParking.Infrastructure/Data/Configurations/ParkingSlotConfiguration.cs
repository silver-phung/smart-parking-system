using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Data.Configurations;

public class ParkingSlotConfiguration : IEntityTypeConfiguration<ParkingSlot>
{
    public void Configure(EntityTypeBuilder<ParkingSlot> builder)
    {
        builder.ToTable("parking_slots", t =>
        {
            t.HasCheckConstraint("chk_parking_slots_physical_status", "physical_status IN ('EMPTY', 'OCCUPIED', 'MAINTENANCE', 'UNAVAILABLE')");
        });

        builder.HasKey(s => s.Id)
            .HasName("pk_parking_slots");

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

        builder.Property(s => s.FloorId)
            .HasColumnName("floor_id")
            .IsRequired();

        builder.HasOne(s => s.Floor)
            .WithMany(f => f.Slots)
            .HasForeignKey(s => s.FloorId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_parking_slots_floor");

        builder.Property(s => s.SlotCode)
            .HasColumnName("slot_code")
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(s => new { s.FloorId, s.SlotCode })
            .IsUnique()
            .HasDatabaseName("uq_parking_slots_floor_slot_code");

        builder.Property(s => s.PhysicalStatus)
            .HasColumnName("physical_status")
            .HasMaxLength(20)
            .HasDefaultValue("EMPTY")
            .IsRequired();

        builder.HasIndex(s => new { s.PhysicalStatus, s.IsActive })
            .HasDatabaseName("ix_parking_slots_status_active");

        builder.Property(s => s.PositionX)
            .HasColumnName("position_x")
            .HasPrecision(10, 2);

        builder.Property(s => s.PositionY)
            .HasColumnName("position_y")
            .HasPrecision(10, 2);

        builder.Property(s => s.PositionZ)
            .HasColumnName("position_z")
            .HasPrecision(10, 2);

        builder.Property(s => s.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

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
