#if EF_REPOSITORIES
// ─────────────────────────────────────────────────────────────────────────────
// EfParkingEventLogRepository.cs
//
// EF Core implementation của IParkingEventLogRepository.
// Kích hoạt khi định nghĩa compile symbol: EF_REPOSITORIES
// (chỉ active sau khi Phụng's feature/phung-database branch được merge vào develop).
//
// TODO (sau merge): Xoá InMemoryParkingEventLogRepository registration trong
//   Infrastructure/DependencyInjection.cs và dùng EfParkingEventLogRepository.
// ─────────────────────────────────────────────────────────────────────────────

using Microsoft.EntityFrameworkCore;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation – ghi và truy vấn ParkingEventLog từ Neon PostgreSQL.
/// Thay thế <see cref="InMemoryParkingEventLogRepository"/> sau khi DB schema ổn định.
/// </summary>
public class EfParkingEventLogRepository : IParkingEventLogRepository
{
    private readonly IApplicationDbContext _db;

    public EfParkingEventLogRepository(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(ParkingEventLog log, CancellationToken ct = default)
    {
        _db.ParkingEventLogs.Add(log);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<ParkingEventLog?> FindByEventIdAsync(
        string eventId, CancellationToken ct = default)
    {
        return await _db.ParkingEventLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.EventId == eventId, ct);
    }

    public async Task<IReadOnlyList<ParkingEventLog>> GetRecentByLotAsync(
        int lotId, int limit, CancellationToken ct = default)
    {
        return await _db.ParkingEventLogs
            .AsNoTracking()
            .Where(l => l.ParkingLotId == lotId)
            .OrderByDescending(l => l.ReceivedAtUtc)
            .Take(limit)
            .ToListAsync(ct);
    }
}
#endif
