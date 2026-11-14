using System.ComponentModel.DataAnnotations;
using ParkingOperations.Domain.Entities;

namespace ParkingOperations.Application.CashHandovers;

public sealed class CreateCashHandoverRequest : IValidatableObject
{
    public Guid OutgoingShiftId { get; init; }
    public Guid IncomingShiftId { get; init; }
    [Range(1, long.MaxValue)] public long OutgoingStaffId { get; init; }
    [Range(1, long.MaxValue)] public long IncomingStaffId { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (OutgoingShiftId == Guid.Empty || IncomingShiftId == Guid.Empty || OutgoingShiftId == IncomingShiftId)
            yield return new ValidationResult("Chọn hai ca khác nhau để bàn giao.", [nameof(OutgoingShiftId), nameof(IncomingShiftId)]);
        if (OutgoingStaffId == IncomingStaffId)
            yield return new ValidationResult("Nhân viên giao và nhận ca phải là hai người khác nhau.", [nameof(IncomingStaffId)]);
    }
}

public sealed class CreateShiftRequest : IValidatableObject
{
    [Range(1, int.MaxValue)] public int LotId { get; init; }
    public DateOnly BusinessDate { get; init; }
    [Required, MaxLength(20)] public string ShiftCode { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (BusinessDate == default)
            yield return new ValidationResult("businessDate phải có giá trị.", [nameof(BusinessDate)]);
        if (!ParkingShiftCodes.IsValid(ShiftCode))
            yield return new ValidationResult("shiftCode phải là MORNING, AFTERNOON hoặc NIGHT.", [nameof(ShiftCode)]);
    }
}

public sealed class AssignStaffToShiftRequest
{
    [Range(1, long.MaxValue)] public long StaffId { get; init; }
    [Required, MaxLength(32)] public string DutyCode { get; init; } = string.Empty;
}

public sealed record ShiftAssignmentDto(Guid Id, long StaffId, string DutyCode, long AssignedByManagerId, DateTime AssignedAtUtc);

public sealed record ParkingShiftDto(
    Guid Id, int LotId, DateOnly BusinessDate, string Code, DateTime StartsAtUtc, DateTime EndsAtUtc,
    string Status, long CreatedByManagerId, DateTime CreatedAtUtc, DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc, int Version, IReadOnlyList<ShiftAssignmentDto> Assignments);

public sealed record ShiftOperationResult(bool Success, int StatusCode, string Message, ParkingShiftDto? Data = null);

public sealed class ConfirmCashHandoverRequest
{
    [Range(typeof(decimal), "0", "999999999999.99")]
    public decimal CountedAmount { get; init; }
}

public sealed class DisputeCashHandoverRequest
{
    [Required, StringLength(500, MinimumLength = 3)]
    public string Reason { get; init; } = string.Empty;
}

public sealed record CashHandoverDto(
    Guid Id,
    int LotId,
    DateOnly BusinessDate,
    string FromShiftCode,
    string ToShiftCode,
    Guid? OutgoingShiftId,
    Guid? IncomingShiftId,
    long OutgoingStaffId,
    long IncomingStaffId,
    long CreatedByManagerId,
    DateTime CreatedAtUtc,
    decimal? OutgoingCountedAmount,
    DateTime? OutgoingConfirmedAtUtc,
    decimal? IncomingCountedAmount,
    DateTime? IncomingConfirmedAtUtc,
    decimal? Variance,
    string? DisputeReason,
    string Status,
    int Version)
{
    public static CashHandoverDto From(CashHandover entity) => new(
        entity.Id, entity.LotId, entity.BusinessDate, entity.FromShiftCode, entity.ToShiftCode,
        entity.OutgoingShiftId, entity.IncomingShiftId,
        entity.OutgoingStaffId, entity.IncomingStaffId, entity.CreatedByManagerId, entity.CreatedAtUtc,
        entity.OutgoingCountedAmount, entity.OutgoingConfirmedAtUtc, entity.IncomingCountedAmount,
        entity.IncomingConfirmedAtUtc,
        entity.OutgoingCountedAmount.HasValue && entity.IncomingCountedAmount.HasValue
            ? entity.IncomingCountedAmount.Value - entity.OutgoingCountedAmount.Value
            : null,
        entity.DisputeReason, entity.Status, entity.Version);
}

public sealed record CashHandoverOperationResult(bool Success, int StatusCode, string Message, CashHandoverDto? Data = null);

public interface ICoreStaffAccessClient
{
    Task<bool> IsCallerAssignedToLotAsync(string bearerToken, int lotId, CancellationToken ct);
    Task<bool> IsActiveStaffAtLotAsync(string bearerToken, int lotId, long staffId, CancellationToken ct);
    Task<bool> AreActiveStaffAtLotAsync(string bearerToken, int lotId, long firstStaffId, long secondStaffId, CancellationToken ct);
}

public interface ICashHandoverService
{
    Task<CashHandoverOperationResult> CreateAsync(CreateCashHandoverRequest request, long managerId, string token, CancellationToken ct);
    Task<(IReadOnlyList<CashHandoverDto> Items, int TotalCount)> ListAsync(int lotId, DateOnly? businessDate, int page, int pageSize, string token, long actorId, string role, CancellationToken ct);
    Task<CashHandoverOperationResult> ConfirmAsync(Guid id, decimal countedAmount, long actorId, string role, string token, CancellationToken ct);
    Task<CashHandoverOperationResult> DisputeAsync(Guid id, string reason, long actorId, string role, string token, CancellationToken ct);
}

public interface IParkingShiftService
{
    Task<(IReadOnlyList<ParkingShiftDto> Items, int TotalCount)> ListAsync(int lotId, DateOnly? businessDate, int page, int pageSize, long actorId, string role, string token, CancellationToken ct);
    Task<ShiftOperationResult> CreateAsync(CreateShiftRequest request, long managerId, string token, CancellationToken ct);
    Task<ShiftOperationResult> AssignAsync(Guid shiftId, AssignStaffToShiftRequest request, long managerId, string token, CancellationToken ct);
    Task<ShiftOperationResult> StartAsync(Guid shiftId, long managerId, string token, CancellationToken ct);
    Task<ShiftOperationResult> CompleteAsync(Guid shiftId, long managerId, string token, CancellationToken ct);
}
