using Microsoft.EntityFrameworkCore;
using SmartParking.Infrastructure.Entities;

namespace SmartParking.Infrastructure.Data;

public sealed class AiRecognitionDbContext : DbContext
{
    public AiRecognitionDbContext(DbContextOptions<AiRecognitionDbContext> options) : base(options) { }

    public DbSet<AiRecognitionRecord> RecognitionRecords => Set<AiRecognitionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AiRecognitionRecord>(entity =>
        {
            entity.ToTable("ai_recognition_records");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PlateNumber).HasMaxLength(32);
            entity.Property(x => x.RawPlateNumber).HasMaxLength(64);
            entity.Property(x => x.FailureReason).HasMaxLength(512);
            entity.Property(x => x.ConfirmedPlateNumber).HasMaxLength(32);
            entity.Property(x => x.ConfirmationNote).HasMaxLength(256);
            entity.Property(x => x.VehicleBboxJson).HasMaxLength(128);
            entity.Property(x => x.PlateBboxJson).HasMaxLength(128);
            entity.HasIndex(x => x.StaffUserId);
            entity.HasIndex(x => x.CreatedAt);
        });
    }
}
