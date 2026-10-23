using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(r => r.Id)
            .HasName("pk_refresh_tokens");

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

        builder.Property(r => r.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.HasOne(r => r.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_refresh_tokens_user");

        builder.Property(r => r.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(r => r.TokenHash)
            .IsUnique()
            .HasDatabaseName("uq_refresh_tokens_hash");

        builder.Property(r => r.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(255);

        builder.Property(r => r.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(45);

        builder.Property(r => r.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(r => r.IsRevoked)
            .HasColumnName("is_revoked")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(r => r.RevokedAt)
            .HasColumnName("revoked_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(r => r.UserId)
            .HasDatabaseName("idx_refresh_tokens_user_id");
    }
}
