using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Data.Configurations;

public class ParkingZoneConfiguration : IEntityTypeConfiguration<ParkingZone>
{
    public void Configure(EntityTypeBuilder<ParkingZone> b)
    {
        b.ToTable("parking_zones", t => {
            t.HasCheckConstraint("ck_zone_type", "\"Type\" IN ('TRANSIENT','FIXED')");
            t.HasCheckConstraint("ck_zone_layout", "\"Layout\" IN ('INSIDE','EXTERNAL')");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.Id, x.LotId });
        b.HasIndex(x => new { x.LotId, x.Code }).IsUnique();
        b.Property(x => x.Code).HasMaxLength(30);
        b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.Type).HasMaxLength(20);
        b.Property(x => x.Layout).HasMaxLength(20);
        b.HasOne(x => x.Lot).WithMany().HasForeignKey(x => x.LotId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ParkingPolicyConfiguration : IEntityTypeConfiguration<ParkingPolicy>
{
    public void Configure(EntityTypeBuilder<ParkingPolicy> b)
    {
        b.ToTable("parking_policies", t => {
            t.HasCheckConstraint("ck_policy_values", "(\"HoldMinutes\" IS NULL OR \"HoldMinutes\" > 0) AND (\"FeeUnitMinutes\" IS NULL OR \"FeeUnitMinutes\" > 0) AND (\"EarlyArrivalMinutes\" IS NULL OR \"EarlyArrivalMinutes\" >= 0) AND (\"LateArrivalMinutes\" IS NULL OR \"LateArrivalMinutes\" >= 0) AND (\"CancellationNoticeMinutes\" IS NULL OR \"CancellationNoticeMinutes\" >= 0) AND (\"DepositAmount\" IS NULL OR \"DepositAmount\" >= 0) AND (\"NearFullThreshold\" IS NULL OR \"NearFullThreshold\" >= 0)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.DepositAmount).HasPrecision(12, 2);
        b.Property(x => x.DecisionReference).HasMaxLength(500);
        b.HasIndex(x => new { x.LotId, x.ZoneId, x.EffectiveFrom });
        b.HasOne<ParkingLot>().WithMany().HasForeignKey(x => x.LotId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Zone).WithMany().HasForeignKey(x => new { x.ZoneId, x.LotId })
            .HasPrincipalKey(x => new { x.Id, x.LotId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ParkingTicketConfiguration : IEntityTypeConfiguration<ParkingTicket>
{
    public void Configure(EntityTypeBuilder<ParkingTicket> b)
    {
        b.ToTable("parking_tickets", t => t.HasCheckConstraint("ck_ticket_status", "\"Status\" IN ('ISSUED','LOST','REPLACED','REVOKED')"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(40);
        b.Property(x => x.Reason).HasMaxLength(500);
        b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("uq_ticket_code");
        b.HasIndex(x => x.SessionId).IsUnique().HasFilter("\"Status\" = 'ISSUED'").HasDatabaseName("uq_ticket_open_session");
        b.HasOne(x => x.Session).WithMany().HasForeignKey(x => new { x.SessionId, x.LotId })
            .HasPrincipalKey(x => new { x.Id, x.LotId }).OnDelete(DeleteBehavior.Restrict);
    }
}
