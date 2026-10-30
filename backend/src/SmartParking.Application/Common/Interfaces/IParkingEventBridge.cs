using SmartParking.Application.Events.Dtos;

namespace SmartParking.Application.Common.Interfaces;

/// <summary>
/// Cổng tích hợp từ projection event sang API nghiệp vụ core.
/// Chỉ gọi sau khi Staff điều phối đã xác minh biển số và actual slot.
/// Entry OCR và exit signal không được bridge tự check-in/check-out.
/// </summary>
public interface IParkingEventBridge
{
    /// <summary>
    /// Đồng bộ kết quả xác minh đỗ qua dispatch/core service có guard và transaction.
    /// </summary>
    Task OnParkingVerifiedAsync(
        IngestEventRequest request,
        string sessionId,
        string authenticatedActorId,
        CancellationToken ct = default);
}
