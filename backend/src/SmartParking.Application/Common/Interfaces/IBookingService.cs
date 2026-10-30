using SmartParking.Application.Common.Models;
using SmartParking.Application.DTOs.Bookings;

namespace SmartParking.Application.Common.Interfaces;

public interface IBookingService
{
    Task<(bool Success, int StatusCode, string Message, BookingDto? Data)> CreateBookingAsync(
        CreateBookingRequestDto request,
        long userId,
        string userRole,
        string? ipAddress,
        CancellationToken ct = default);

    Task<PagedResult<BookingDto>> GetMyBookingsAsync(
        long userId,
        BookingFilterDto filter,
        CancellationToken ct = default);

    Task<(bool Success, int StatusCode, string Message, BookingDetailDto? Data)> GetBookingByIdAsync(
        long bookingId,
        long currentUserId,
        string currentUserRole,
        CancellationToken ct = default);

    Task<(bool Success, int StatusCode, string Message, BookingDto? Data)> CancelBookingAsync(
        long bookingId,
        CancelBookingRequestDto request,
        long currentUserId,
        string currentUserRole,
        string? ipAddress,
        CancellationToken ct = default);

    Task<PagedResult<BookingDto>> GetBookingsAsync(
        BookingFilterDto filter,
        long currentUserId,
        string currentUserRole,
        CancellationToken ct = default);

    Task<LotCapacityDto?> GetLotCapacityAsync(
        int lotId,
        long? currentUserId = null,
        string? currentUserRole = null,
        CancellationToken ct = default);
}
