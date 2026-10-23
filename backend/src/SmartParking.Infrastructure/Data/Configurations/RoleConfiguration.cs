using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Data.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");

        builder.HasKey(r => r.Id)
            .HasName("pk_roles");

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

        builder.Property(r => r.Code)
            .HasColumnName("code")
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(r => r.Code)
            .IsUnique()
            .HasDatabaseName("uq_roles_code");

        builder.Property(r => r.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.Description)
            .HasColumnName("description")
            .HasMaxLength(255);

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()")
            .IsRequired();

        // Seed core roles
        builder.HasData(
            new Role { Id = 1, Code = "ADMIN", Name = "Quản trị viên", Description = "Toàn quyền quản trị hệ thống Smart Parking", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Role { Id = 2, Code = "OPERATOR", Name = "Nhân viên điều hành", Description = "Nhân viên vận hành bãi xe, trực cổng, xử lý điều phối", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Role { Id = 3, Code = "CUSTOMER", Name = "Khách hàng", Description = "Khách hàng gửi xe cá nhân", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}
