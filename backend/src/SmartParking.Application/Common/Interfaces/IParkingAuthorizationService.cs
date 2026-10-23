namespace SmartParking.Application.Common.Interfaces;

public interface IParkingAuthorizationService
{
    Task<bool> HasLotAccessAsync(long userId, string userRole, int lotId, CancellationToken ct = default);

    Task<List<int>> GetAssignedLotIdsAsync(long userId, string userRole, CancellationToken ct = default);

    Task<bool> CanManageLotStatusAsync(long userId, string userRole, int lotId, CancellationToken ct = default);

    Task<bool> CanManageSlotStatusAsync(long userId, string userRole, int lotId, CancellationToken ct = default);

    Task<bool> CanOperateSessionOrDispatchAsync(long userId, string userRole, int lotId, CancellationToken ct = default);
}
