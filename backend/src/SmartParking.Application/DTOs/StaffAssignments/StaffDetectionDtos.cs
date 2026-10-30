namespace SmartParking.Application.DTOs.StaffAssignments;

public sealed record StaffDetectionDto(
    long Id,
    long StaffUserId,
    string? PlateNumber,
    string? RawPlateNumber,
    string Status,
    string? FailureReason,
    double Confidence,
    bool IsLegit,
    double? VehicleConfidence,
    IReadOnlyList<int>? VehicleBbox,
    IReadOnlyList<int>? PlateBbox,
    double InferenceTimeMs,
    DateTime CreatedAt,
    string? ConfirmedPlateNumber = null,
    long? ConfirmedByStaffUserId = null,
    DateTime? ConfirmedAt = null,
    string? ConfirmationNote = null);

public sealed record ConfirmStaffDetectionRequestDto(
    string PlateNumber,
    string? Note);
