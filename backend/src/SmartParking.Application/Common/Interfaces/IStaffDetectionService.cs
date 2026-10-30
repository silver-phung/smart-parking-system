using SmartParking.Application.DTOs.StaffAssignments;

namespace SmartParking.Application.Common.Interfaces;

public interface IStaffDetectionService
{
    Task<StaffDetectionDto> DetectAsync(
        Stream image,
        string contentType,
        long contentLength,
        long staffUserId,
        CancellationToken cancellationToken);

    Task<StaffDetectionDto?> ConfirmAsync(
        long recognitionId,
        string plateNumber,
        string? note,
        long staffUserId,
        CancellationToken cancellationToken);
}
