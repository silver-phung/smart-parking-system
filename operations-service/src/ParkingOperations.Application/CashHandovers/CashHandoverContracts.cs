using System.ComponentModel.DataAnnotations;
using ParkingOperations.Domain.Entities;

namespace ParkingOperations.Application.CashHandovers;

public sealed class CreateCashHandoverRequest : IValidatableObject
{
    [Range(1, int.MaxValue)] public int LotId { get; init; }
    public DateOnly BusinessDate { get; init; }
    [Required, MaxLength(20)] public string FromShiftCode { get; init; } = string.Empty;
    [Required, MaxLength(20)] public string ToShiftCode { get; init; } = string.Empty;
    [Range(1, long.MaxValue)] public long OutgoingStaffId { get; init; }
    [Range(1, long.MaxValue)] public long IncomingStaffId { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (BusinessDate == default)
            yield return new ValidationResult("businessDate phải có giá trị.", [nameof(BusinessDate)]);
        if (!ParkingShiftCodes.IsValid(FromShiftCode) || !ParkingShiftCodes.IsValid(ToShiftCode) ||
            !ParkingShiftCodes.IsNext(FromShiftCode, ToShiftCode))
            yield return new ValidationResult("Chọn đúng hai ca liền kề theo thứ tự MORNING → AFTERNOON → NIGHT → MORNING.", [nameof(FromShiftCode), nameof(ToShiftCode)]);
        if (OutgoingStaffId == IncomingStaffId)
            yield return new ValidationResult("Nhân viên giao và nhận ca phải là hai người khác nhau.", [nameof(IncomingStaffId)]);
    }
}

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
    Task<bool> AreActiveStaffAtLotAsync(string bearerToken, int lotId, long firstStaffId, long secondStaffId, CancellationToken ct);
}

public interface ICashHandoverService
{
    Task<CashHandoverOperationResult> CreateAsync(CreateCashHandoverRequest request, long managerId, string token, CancellationToken ct);
    Task<(IReadOnlyList<CashHandoverDto> Items, int TotalCount)> ListAsync(int lotId, DateOnly? businessDate, int page, int pageSize, string token, long actorId, string role, CancellationToken ct);
    Task<CashHandoverOperationResult> ConfirmAsync(Guid id, decimal countedAmount, long actorId, string role, string token, CancellationToken ct);
    Task<CashHandoverOperationResult> DisputeAsync(Guid id, string reason, long actorId, string role, string token, CancellationToken ct);
}
