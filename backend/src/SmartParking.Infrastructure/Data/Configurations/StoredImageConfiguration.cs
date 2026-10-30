using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Data.Configurations;

public class StoredImageConfiguration : IEntityTypeConfiguration<StoredImage>
{
    public void Configure(EntityTypeBuilder<StoredImage> builder)
    {
        builder.ToTable("stored_images");

        builder.HasKey(image => image.Id)
            .HasName("pk_stored_images");

        builder.Property(image => image.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

        builder.Property(image => image.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.HasOne(image => image.User)
            .WithMany(user => user.StoredImages)
            .HasForeignKey(image => image.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_stored_images_user");

        builder.Property(image => image.PublicId)
            .HasColumnName("public_id")
            .HasMaxLength(255)
            .IsRequired();

        builder.HasIndex(image => image.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_stored_images_public_id");

        builder.Property(image => image.Url)
            .HasColumnName("url")
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(image => image.Format)
            .HasColumnName("format")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(image => image.Bytes)
            .HasColumnName("bytes")
            .IsRequired();

        builder.Property(image => image.Width)
            .HasColumnName("width")
            .IsRequired();

        builder.Property(image => image.Height)
            .HasColumnName("height")
            .IsRequired();

        builder.Property(image => image.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(image => image.UserId)
            .HasDatabaseName("ix_stored_images_user_id");
    }
}
