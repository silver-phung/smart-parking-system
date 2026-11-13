namespace SmartParking.Application.DTOs.Operations;

public record ZoneRequest(string Code, string Name, string Type, string Layout, bool IsActive,
    int[]? FloorIds = null, ZoneFloorRequest? NewFloor = null, int? FloorId = null);
public record ZoneFloorRequest(string Name, int FloorNumber);
public record FloorZoneRequest(int ZoneId);
public record SlotFuelRequest(string AllowedFuelType);
public record EntryRequest(Guid EventId, int ZoneId, string TicketCode, long? ImageId, string? ManualReason);
public record VerifyVehicleRequest(long ActualSlotId, string PlateNumber, string FuelType,
    long ExpectedRevision, bool VehicleObserved, string? Reason);
public record MoveVehicleRequest(long ActualSlotId, long ExpectedRevision, bool OldSlotObservedEmpty,
    bool VehicleObservedAtDestination, string Reason);
public record TicketChangeRequest(string Action, string? ReplacementCode, string Reason);
public record PolicyRequest(int? ZoneId, DateTime EffectiveFrom, bool Approve, string DecisionReference,
    int? HoldMinutes, int? EarlyArrivalMinutes, int? LateArrivalMinutes,
    int? CancellationNoticeMinutes, decimal? DepositAmount, int? NearFullThreshold,
    int? FeeUnitMinutes, string? FeeRounding, string? NoShowAction,
    string? CancellationAction, string? RateSelection);
public record OperationSessionDto(long Id, int LotId, int? ZoneId, long? PolicyId, Guid? EventId,
    string PlateNumber, string? TemporaryPlateNumber, long? ImageId,
    string VerificationStatus, string Status, string? FuelType, long? CurrentSlotId,
    string? SlotCode, long Revision, DateTime CheckInTime, DateTime? VerifiedAt,
    string? TicketCode, string? TicketStatus);

public sealed class ParkingOperationException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
