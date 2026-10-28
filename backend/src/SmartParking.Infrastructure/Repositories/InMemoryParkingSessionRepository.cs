using System.Collections.Concurrent;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Repositories;

/// <summary>
/// In-memory repository cho EventParkingSession.
///
/// Sprint 3 note: Dùng in-memory để unblock phát triển khi chưa có DB schema chính thức.
/// Khi Trí hoàn thành migration PostgreSQL/Neon, thay bằng EfParkingSessionRepository.
/// </summary>
public class InMemoryParkingSessionRepository : IParkingSessionRepository
{
    // Key = sessionId
    private static readonly ConcurrentDictionary<string, EventParkingSession> _bySessionId
        = new(StringComparer.OrdinalIgnoreCase);

    private static long _idCounter = 0;

    public Task<EventParkingSession?> FindBySessionIdAsync(string sessionId, CancellationToken ct = default)
    {
        _bySessionId.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    public Task<EventParkingSession?> FindActiveByCarPlateAsync(string carPlate, CancellationToken ct = default)
    {
        var normalized = carPlate.Trim().ToUpperInvariant();
        var session = _bySessionId.Values.FirstOrDefault(s =>
            s.CarPlate.Equals(normalized, StringComparison.OrdinalIgnoreCase) &&
            (s.Status == ParkingSessionStatus.PendingSlot ||
             s.Status == ParkingSessionStatus.Parked));

        return Task.FromResult(session);
    }

    public Task AddAsync(EventParkingSession session, CancellationToken ct = default)
    {
        session.Id = Interlocked.Increment(ref _idCounter);
        session.CreatedAtUtc = DateTimeOffset.UtcNow;
        session.UpdatedAtUtc = DateTimeOffset.UtcNow;

        _bySessionId[session.SessionId] = session;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(EventParkingSession session, CancellationToken ct = default)
    {
        session.UpdatedAtUtc = DateTimeOffset.UtcNow;
        _bySessionId[session.SessionId] = session;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<EventParkingSession>> GetActiveByLotAsync(
        int parkingLotId, CancellationToken ct = default)
    {
        IReadOnlyList<EventParkingSession> result = _bySessionId.Values
            .Where(s =>
                s.ParkingLotId == parkingLotId &&
                (s.Status == ParkingSessionStatus.PendingSlot ||
                 s.Status == ParkingSessionStatus.Parked))
            .OrderBy(s => s.EnteredAtUtc)
            .ToList();

        return Task.FromResult(result);
    }
}
