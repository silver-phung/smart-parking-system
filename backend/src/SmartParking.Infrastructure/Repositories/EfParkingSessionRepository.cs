#if EF_REPOSITORIES
// ─────────────────────────────────────────────────────────────────────────────
// EfParkingSessionRepository.cs
//
// EF Core implementation của IParkingSessionRepository (module Thức).
//
// LƯU Ý quan trọng về conflict với Phụng's ParkingSession entity:
//   • Phụng's entity dùng: LotId, PlateNumber, Status = "ACTIVE/PAID/COMPLETED"
//   • Event projection dùng: waiting_verification/parked/exit_processing.
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

    public async Task<EventParkingSession?> FindBySessionIdAsync(
        string sessionId, CancellationToken ct = default)
    {
        return await _db.EventParkingSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, ct);
    }

    public async Task<EventParkingSession?> FindActiveByCarPlateAsync(
        string carPlate, CancellationToken ct = default)
    {
        var normalized = carPlate.Trim().ToUpperInvariant();
        return await _db.EventParkingSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s =>
                s.CarPlate != "" && s.CarPlate == normalized, ct);
    }

    public async Task<EventParkingSession?> FindActiveBySlotAsync(
        int parkingLotId, string slotCode, CancellationToken ct = default)
    {
        return await _db.EventParkingSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s =>
                s.ParkingLotId == parkingLotId &&
                s.Status == ParkingSessionStatus.Parked &&
                s.SlotCode == slotCode, ct);
    }

    public async Task AddAsync(EventParkingSession session, CancellationToken ct = default)
    {
        _db.EventParkingSessions.Add(session);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(EventParkingSession session, CancellationToken ct = default)
    {
        session.UpdatedAtUtc = DateTimeOffset.UtcNow;
        _db.EventParkingSessions.Update(session);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<EventParkingSession>> GetActiveByLotAsync(
        int parkingLotId, CancellationToken ct = default)
    {
        return await _db.EventParkingSessions
            .AsNoTracking()
            .Where(s =>
                s.ParkingLotId == parkingLotId &&
                (s.Status == ParkingSessionStatus.WaitingVerification ||
                 s.Status == ParkingSessionStatus.Parked ||
                 s.Status == ParkingSessionStatus.ExitProcessing))
            .OrderBy(s => s.EnteredAtUtc)
            .ToListAsync(ct);
    }
}
#endif
