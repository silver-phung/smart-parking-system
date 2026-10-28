using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartParking.Domain.Constants;
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

        // Seed core roles: ADMIN, MANAGER, STAFF, USER
        builder.HasData(
            new Role { Id = 1, Code = Roles.Admin, Name = "Quản trị viên", Description = "Toàn quyền quản trị hệ thống Smart Parking", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Role { Id = 2, Code = Roles.Manager, Name = "Quản lý bãi đỗ", Description = "Quản lý vận hành bãi xe, cấu hình giá, nhân sự và báo cáo doanh thu", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Role { Id = 3, Code = Roles.Staff, Name = "Nhân viên vận hành", Description = "Nhân viên trực cổng, kiểm soát vào ra và hỗ trợ xử lý sự cố", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Role { Id = 4, Code = Roles.User, Name = "Người dùng", Description = "Người dùng tự đăng ký tài khoản, gửi xe và thanh toán cá nhân", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}
