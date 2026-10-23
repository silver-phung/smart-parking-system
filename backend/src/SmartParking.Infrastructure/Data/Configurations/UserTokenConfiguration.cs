using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Data.Configurations;

public class UserTokenConfiguration : IEntityTypeConfiguration<UserToken>
{
    public void Configure(EntityTypeBuilder<UserToken> builder)
    {
        builder.ToTable("user_tokens", t =>
        {
            t.HasCheckConstraint("chk_user_tokens_type", "token_type IN ('EMAIL_VERIFICATION', 'PASSWORD_RESET')");
            t.HasCheckConstraint("chk_user_tokens_attempts", "attempts_count >= 0");
        });

        builder.HasKey(t => t.Id)
            .HasName("pk_user_tokens");

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

        builder.Property(t => t.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.HasOne(t => t.User)
            .WithMany(u => u.UserTokens)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_user_tokens_user");

        builder.Property(t => t.TokenType)
            .HasColumnName("token_type")
            .HasMaxLength(50)
            .IsRequired();

        // [Security Audit C1]: NO global unique constraint on token_hash
        builder.Property(t => t.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(t => t.AttemptsCount)
            .HasColumnName("attempts_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(t => t.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(t => t.IsUsed)
            .HasColumnName("is_used")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(t => t.UsedAt)
            .HasColumnName("used_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()")
            .IsRequired();

        // Index for looking up active tokens by user and type
        builder.HasIndex(t => new { t.UserId, t.TokenType })
            .HasDatabaseName("idx_user_tokens_user_type");
    }
}
