using System.Collections.Concurrent;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Repositories;

/// <summary>
/// In-memory repository cho ParkingEventLog.
///
/// Sprint 3 note: Dùng in-memory để unblock phát triển khi chưa có DB schema chính thức.
/// Khi Trí hoàn thành migration PostgreSQL/Neon, thay bằng EfParkingEventLogRepository.
/// Đảm bảo ràng buộc UNIQUE(event_id) được giữ bằng ConcurrentDictionary.
/// </summary>
public class InMemoryParkingEventLogRepository : IParkingEventLogRepository
{
    // Key = eventId – đảm bảo unique giống ràng buộc DB
    private static readonly ConcurrentDictionary<string, ParkingEventLog> _store
        = new(StringComparer.OrdinalIgnoreCase);

    private static long _idCounter = 0;

    public Task<ParkingEventLog?> FindByEventIdAsync(string eventId, CancellationToken ct = default)
    {
        _store.TryGetValue(eventId, out var log);
        return Task.FromResult(log);
    }

    public Task AddAsync(ParkingEventLog log, CancellationToken ct = default)
    {
        log.Id = Interlocked.Increment(ref _idCounter);
        log.ReceivedAtUtc = DateTimeOffset.UtcNow;

        // TryAdd – nếu EventId đã tồn tại thì bỏ qua (idempotency tầng in-memory)
        _store.TryAdd(log.EventId, log);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ParkingEventLog>> GetRecentByLotAsync(
        int parkingLotId, int limit = 100, CancellationToken ct = default)
    {
        IReadOnlyList<ParkingEventLog> result = _store.Values
            .Where(x => x.ParkingLotId == parkingLotId)
            .OrderByDescending(x => x.ReceivedAtUtc)
            .Take(limit)
            .ToList();

        return Task.FromResult(result);
    }
}
