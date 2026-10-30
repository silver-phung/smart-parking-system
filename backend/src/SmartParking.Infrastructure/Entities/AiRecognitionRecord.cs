namespace SmartParking.Infrastructure.Entities;

public sealed class AiRecognitionRecord
{
    public long Id { get; set; }
    public long StaffUserId { get; set; }
    public string? PlateNumber { get; set; }
    public string? RawPlateNumber { get; set; }
    public string? FailureReason { get; set; }
    public string? ConfirmedPlateNumber { get; set; }
    public long? ConfirmedByStaffUserId { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? ConfirmationNote { get; set; }
    public double Confidence { get; set; }
    public bool IsLegit { get; set; }
    public double? VehicleConfidence { get; set; }
    public string? VehicleBboxJson { get; set; }
    public string? PlateBboxJson { get; set; }
    public double InferenceTimeMs { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
