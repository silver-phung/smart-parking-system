#if EF_REPOSITORIES
// ─────────────────────────────────────────────────────────────────────────────
// EfParkingSessionRepository.cs
//
// EF Core implementation của IParkingSessionRepository (module Thức).
//
// LƯU Ý quan trọng về conflict với Phụng's ParkingSession entity:
//   • Phụng's entity dùng: LotId, PlateNumber, Status = "ACTIVE/PAID/COMPLETED"
//   • Thức's entity dùng: ParkingLotId, CarPlate, Status = "pending_slot/parked/exited"
//
// Chiến lược tích hợp (sau merge):
//   Option A – Dùng bảng riêng "parking_event_sessions" cho module Thức (khuyến nghị)
//   Option B – Map Thức's ParkingSession sang Phụng's ParkingSession (phức tạp hơn)
//
// File này implement Option A: dùng bảng riêng, không đụng Phụng's bảng "parking_sessions".
// ─────────────────────────────────────────────────────────────────────────────

using Microsoft.EntityFrameworkCore;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Repositories;

/// <summary>
/// EF Core repository cho ParkingSession (module Event Ingestion của Thức).
/// Dùng bảng <c>parking_event_sessions</c> riêng biệt để không xung đột với
/// bảng <c>parking_sessions</c> của module Phụng.
/// </summary>
public class EfParkingSessionRepository : IParkingSessionRepository
{
    private readonly IApplicationDbContext _db;

    public EfParkingSessionRepository(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ParkingSession?> FindBySessionIdAsync(
        string sessionId, CancellationToken ct = default)
    {
        return await _db.EventParkingSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, ct);
    }

    public async Task<ParkingSession?> FindActiveByCarPlateAsync(
        string carPlate, CancellationToken ct = default)
    {
        var normalized = carPlate.Trim().ToUpperInvariant();
        return await _db.EventParkingSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s =>
                s.CarPlate == normalized &&
                (s.Status == ParkingSessionStatus.PendingSlot ||
                 s.Status == ParkingSessionStatus.Parked), ct);
    }

    public async Task AddAsync(ParkingSession session, CancellationToken ct = default)
    {
        _db.EventParkingSessions.Add(session);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ParkingSession session, CancellationToken ct = default)
    {
        session.UpdatedAtUtc = DateTimeOffset.UtcNow;
        _db.EventParkingSessions.Update(session);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ParkingSession>> GetActiveByLotAsync(
        int parkingLotId, CancellationToken ct = default)
    {
        return await _db.EventParkingSessions
            .AsNoTracking()
            .Where(s =>
                s.ParkingLotId == parkingLotId &&
                (s.Status == ParkingSessionStatus.PendingSlot ||
                 s.Status == ParkingSessionStatus.Parked))
            .OrderBy(s => s.EnteredAtUtc)
            .ToListAsync(ct);
    }
}
#endif
