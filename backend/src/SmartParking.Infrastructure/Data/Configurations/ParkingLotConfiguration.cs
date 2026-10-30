using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Data.Configurations;

public class ParkingLotConfiguration : IEntityTypeConfiguration<ParkingLot>
{
    public void Configure(EntityTypeBuilder<ParkingLot> builder)
    {
        builder.ToTable("parking_lots", t =>
        {
            t.HasCheckConstraint("chk_parking_lots_capacity", "total_capacity >= 0");
        });

        builder.HasKey(l => l.Id)
            .HasName("pk_parking_lots");

        builder.Property(l => l.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

        builder.Property(l => l.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(l => l.Name)
            .IsUnique()
            .HasDatabaseName("uq_parking_lots_name");

        builder.Property(l => l.Address)
            .HasColumnName("address")
            .HasMaxLength(255);

        builder.Property(l => l.TotalCapacity)
            .HasColumnName("total_capacity")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(l => l.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(l => l.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(l => l.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();
    }
}
