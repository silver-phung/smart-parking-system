using SmartParking.Application.DTOs.StaffAssignments;

namespace SmartParking.Application.Common.Interfaces;

public interface IStaffDetectionService
{
    Task<StaffDetectionDto> DetectAsync(
        Stream image,
        string contentType,
        long contentLength,
        long staffUserId,
        int lotId,
        string purpose,
        CancellationToken cancellationToken);

    Task<StaffDetectionDto?> ConfirmAsync(
        long recognitionId,
        string plateNumber,
        string? note,
        long staffUserId,
        int lotId,
        CancellationToken cancellationToken);

    Task<StaffDetectionDto?> ConfirmExitAsync(
        long recognitionId,
        long sessionId,
        string plateNumber,
        string? note,
        long staffUserId,
        int lotId,
        CancellationToken cancellationToken);
}
