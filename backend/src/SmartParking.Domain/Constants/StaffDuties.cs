namespace SmartParking.Domain.Constants;

public static class StaffDuties
{
    public const string Gate = "GATE";
    public const string GateIn = "GATE_IN";
    public const string GateOut = "GATE_OUT";
    public const string SlotVerifier = "SLOT_VERIFIER";
    public const string Both = "BOTH";

    public static readonly IReadOnlyList<string> All = [GateIn, GateOut, Gate, SlotVerifier, Both];

    public static bool IsValid(string? duty) =>
        !string.IsNullOrWhiteSpace(duty) && All.Contains(duty.Trim().ToUpperInvariant());

    // Hai loại phân công: trực cổng (GATE: vào + ra) và trực khu vực (SLOT_VERIFIER).
    // GATE_IN / GATE_OUT vẫn được chấp nhận để tương thích dữ liệu và API cũ.
    public static bool IsAssignable(string? duty) => duty is Gate or GateIn or GateOut or SlotVerifier;
}
