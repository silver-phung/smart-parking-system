using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Data.Configurations;

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicles", t =>
        {
            t.HasCheckConstraint("chk_vehicles_plate_normalized", "plate_number = UPPER(TRIM(plate_number))");
        });

        builder.HasKey(v => v.Id)
            .HasName("pk_vehicles");

        builder.Property(v => v.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

        builder.Property(v => v.UserId)
            .HasColumnName("user_id");

        builder.HasIndex(v => v.UserId)
            .HasDatabaseName("ix_vehicles_user_id");

        builder.HasOne(v => v.User)
            .WithMany(u => u.Vehicles)
            .HasForeignKey(v => v.UserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_vehicles_user");

        builder.Property(v => v.PlateNumber)
            .HasColumnName("plate_number")
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(v => v.PlateNumber)
            .IsUnique()
            .HasDatabaseName("uq_vehicles_plate_number");

        builder.Property(v => v.Brand)
            .HasColumnName("brand")
            .HasMaxLength(50);

        builder.Property(v => v.Model)
            .HasColumnName("model")
            .HasMaxLength(50);

        builder.Property(v => v.Color)
            .HasColumnName("color")
            .HasMaxLength(30);

        builder.Property(v => v.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(v => v.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(v => v.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();
    }
}
