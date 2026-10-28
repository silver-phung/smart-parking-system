using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Data.Configurations;

public class ParkingFloorConfiguration : IEntityTypeConfiguration<ParkingFloor>
{
    public void Configure(EntityTypeBuilder<ParkingFloor> builder)
    {
        builder.ToTable("parking_floors", t =>
        {
            t.HasCheckConstraint("chk_parking_floors_slots", "total_slots >= 0");
        });

        builder.HasKey(f => f.Id)
            .HasName("pk_parking_floors");

        builder.Property(f => f.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

        builder.Property(f => f.LotId)
            .HasColumnName("lot_id")
            .IsRequired();

        builder.HasOne(f => f.Lot)
            .WithMany(l => l.Floors)
            .HasForeignKey(f => f.LotId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_parking_floors_lot");

        builder.Property(f => f.FloorNumber)
            .HasColumnName("floor_number")
            .IsRequired();

        builder.Property(f => f.Name)
            .HasColumnName("name")
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(f => new { f.LotId, f.FloorNumber })
            .IsUnique()
            .HasDatabaseName("uq_parking_floors_lot_floor_number");

        builder.HasIndex(f => new { f.LotId, f.Name })
            .IsUnique()
            .HasDatabaseName("uq_parking_floors_lot_name");

        builder.Property(f => f.TotalSlots)
            .HasColumnName("total_slots")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(f => f.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(f => f.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(f => f.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();
    }
}
