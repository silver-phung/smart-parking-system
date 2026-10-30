namespace SmartParking.Domain.Constants;

public static class StaffDuties
{
    public const string Gate = "GATE";
    public const string SlotVerifier = "SLOT_VERIFIER";
    public const string Both = "BOTH";

    public static readonly IReadOnlyList<string> All = [Gate, SlotVerifier, Both];

    public static bool IsValid(string? duty) =>
        !string.IsNullOrWhiteSpace(duty) && All.Contains(duty.Trim().ToUpperInvariant());
}
