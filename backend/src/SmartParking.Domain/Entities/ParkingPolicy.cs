namespace SmartParking.Domain.Entities;

// Append-only versions. A draft never supplies operational defaults.
public class ParkingPolicy
{
    public long Id { get; set; }
    public int LotId { get; set; }
    public int? ZoneId { get; set; }
    public ParkingZone? Zone { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long CreatedBy { get; set; }
    public long? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string DecisionReference { get; set; } = "";
    public int? HoldMinutes { get; set; }
    public int? EarlyArrivalMinutes { get; set; }
    public int? LateArrivalMinutes { get; set; }
    public int? CancellationNoticeMinutes { get; set; }
    public decimal? DepositAmount { get; set; }
    public int? NearFullThreshold { get; set; }
    public int? FeeUnitMinutes { get; set; }
    public string? FeeRounding { get; set; }
    public string? NoShowAction { get; set; }
    public string? CancellationAction { get; set; }
    public string? RateSelection { get; set; }
    public string SlotReleaseRule { get; set; } = "VERIFIED_EXIT";
}
