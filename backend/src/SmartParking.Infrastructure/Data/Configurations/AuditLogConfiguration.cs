using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(a => a.Id)
            .HasName("pk_audit_logs");

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

        builder.Property(a => a.ActorId)
            .HasColumnName("actor_id");

        builder.HasOne(a => a.Actor)
            .WithMany(u => u.AuditLogs)
            .HasForeignKey(a => a.ActorId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_audit_logs_actor");

        builder.Property(a => a.Action)
            .HasColumnName("action")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.TargetEntity)
            .HasColumnName("target_entity")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.TargetId)
            .HasColumnName("target_id")
            .HasMaxLength(50);

        builder.Property(a => a.OldValues)
            .HasColumnName("old_values")
            .HasColumnType("text");

        builder.Property(a => a.NewValues)
            .HasColumnName("new_values")
            .HasColumnType("text");

        builder.Property(a => a.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(45);

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(a => a.ActorId)
            .HasDatabaseName("ix_audit_logs_actor_id");

        builder.HasIndex(a => new { a.TargetEntity, a.TargetId })
            .HasDatabaseName("ix_audit_logs_target");

        builder.HasIndex(a => a.CreatedAt)
            .HasDatabaseName("ix_audit_logs_created_at");
    }
}
