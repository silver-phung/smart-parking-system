using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.Application.DTOs.Operations;
using SmartParking.Application.DTOs.Sessions;
using SmartParking.Domain.Entities;
using SmartParking.Infrastructure.Common;
using SmartParking.Infrastructure.Data;
using SmartParking.Infrastructure.Entities;
using SmartParking.Infrastructure.Services;

namespace SmartParking.Tests;

public class ParkingOperationsTests
{
    private sealed class Fixture : IAsyncDisposable
    {
        public TestSmartParkingDbContext Db { get; }
        public AiRecognitionDbContext Ai { get; }
        public ParkingOperationsService Service { get; }
        public Fixture(TestSmartParkingDbContext db)
        {
            Db = db;
            Ai = new AiRecognitionDbContext(new DbContextOptionsBuilder<AiRecognitionDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            Service = new ParkingOperationsService(db, new ParkingAuthorizationService(db), new RecognitionEvidenceReader(Ai));
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Ai.DisposeAsync(); }
        public Task<OperationSessionDto> Enter(string code = "VE-001", Guid? eventId = null, long? recognition = null, long? image = null) =>
            Service.Enter(1, new(eventId ?? Guid.NewGuid(), 1, code, recognition, image, "Kiểm tra trực tiếp khi AI chưa sẵn sàng"), 1, "ADMIN", default);
        public Task<OperationSessionDto> Verify(OperationSessionDto s, long slot = 1, string plate = "51F-123.45", string fuel = "PETROL") =>
            Service.Verify(1, s.Id, new(slot, plate, fuel, s.Revision, true, "Đối chiếu xe thực tế"), 1, "ADMIN", default);
    }
    private static async Task<Fixture> Create()
    {
        var db = await TestDbHelper.CreateInMemoryDbContextAsync();
        db.Users.AddRange(new User { Id = 1, Username = "admin_ops", Email = "admin@ops.test", FullName = "Admin", PasswordHash = "x", RoleId = 1 },
            new User { Id = 2, Username = "gate_ops", Email = "gate@ops.test", FullName = "Gate", PasswordHash = "x", RoleId = 3 });
        db.ParkingLots.AddRange(new ParkingLot { Id = 1, Name = "Test", IsActive = true }, new ParkingLot { Id = 2, Name = "Other", IsActive = true });
        db.ParkingZones.AddRange(new ParkingZone { Id = 1, LotId = 1, Code = "VL", Name = "Vãng lai" },
            new ParkingZone { Id = 2, LotId = 1, Code = "CD", Name = "Cố định", Type = "FIXED", Layout = "EXTERNAL" });
        db.ParkingFloors.AddRange(new ParkingFloor { Id = 1, LotId = 1, ZoneId = 1, Name = "Tầng 1" },
            new ParkingFloor { Id = 2, LotId = 1, ZoneId = 2, Name = "Ngoài trời", FloorNumber = 2 });
        db.ParkingSlots.AddRange(new ParkingSlot { Id = 1, FloorId = 1, SlotCode = "A1" },
            new ParkingSlot { Id = 2, FloorId = 1, SlotCode = "A2" },
            new ParkingSlot { Id = 3, FloorId = 1, SlotCode = "A3", AllowedFuelType = "ELECTRIC" },
            new ParkingSlot { Id = 4, FloorId = 2, SlotCode = "FIXED1" });
        db.StaffLotAssignments.Add(new StaffLotAssignment { UserId = 2, LotId = 1, AssignedBy = 1, Duty = "GATE" });
        db.ParkingPolicies.Add(new ParkingPolicy { Id = 1, LotId = 1, ApprovedAt = DateTime.UtcNow, ApprovedBy = 1,
            CreatedBy = 1, EffectiveFrom = DateTime.UtcNow.AddHours(-1), NearFullThreshold = 1 });
        await db.SaveChangesAsync();
        return new Fixture(db);
    }
    [Fact]
    public async Task Replayed_event_returns_same_ticket_and_session_and_rejects_different_payload()
    {
        await using var f = await Create(); var eventId = Guid.NewGuid();
        var first = await f.Enter(eventId: eventId);
        var replay = await f.Enter(eventId: eventId);
        Assert.Equal(first.Id, replay.Id);
        Assert.Single(await f.Db.ParkingSessions.ToListAsync());
        Assert.Single(await f.Db.ParkingTickets.ToListAsync());
        var ex = await Assert.ThrowsAsync<ParkingOperationException>(() => f.Enter("OTHER", eventId));
        Assert.Equal("EVENT_MISMATCH", ex.Code);
    }
    [Fact]
    public async Task Temporary_ocr_duplicates_are_separate_waiting_sessions_and_count_capacity()
    {
        await using var f = await Create();
        f.Ai.RecognitionRecords.AddRange(new AiRecognitionRecord { Id = 1, StaffUserId = 1, PlateNumber = "51F12345", ContentSha256 = "same" },
            new AiRecognitionRecord { Id = 2, StaffUserId = 1, PlateNumber = "51F12345", ContentSha256 = "same" });
        f.Db.StoredImages.Add(new StoredImage { Id = 1, UserId = 1, PublicId = "img1", Url = "https://example.test/1", ContentSha256 = "same" });
        await f.Ai.SaveChangesAsync(); await f.Db.SaveChangesAsync();
        var a = await f.Enter(recognition: 1, image: 1); var b = await f.Enter("VE-002", recognition: 2, image: 1);
        Assert.NotEqual(a.Id, b.Id); Assert.Equal("", a.PlateNumber); Assert.Equal("WAITING_VERIFICATION", a.VerificationStatus);
        Assert.All(await f.Db.ParkingSlots.ToListAsync(), s => Assert.Equal("EMPTY", s.PhysicalStatus));
        var capacity = await ParkingLotCapacityHelper.CalculateAsync(f.Db, 1);
        Assert.Equal(3, capacity.TotalCapacity); Assert.Equal(2, capacity.ActiveSessionsCount); Assert.Equal(1, capacity.AvailableCapacity);
    }
    [Fact]
    public async Task Fixed_zone_does_not_add_walk_in_capacity_and_no_slots_never_accepts_entry()
    {
        await using var f = await Create();
        f.Db.ParkingSlots.RemoveRange(f.Db.ParkingSlots.Where(s => s.FloorId == 1)); await f.Db.SaveChangesAsync();
        var calc = await ParkingLotCapacityHelper.CalculateAsync(f.Db, 1);
        Assert.Equal(0, calc.TotalCapacity);
        Assert.Equal("CAPACITY_EXHAUSTED", (await Assert.ThrowsAsync<ParkingOperationException>(() => f.Enter())).Code);
    }
    [Fact]
    public async Task Verification_sets_official_plate_and_occupancy_atomically_without_changing_session_id()
    {
        await using var f = await Create(); var entry = await f.Enter(); var verified = await f.Verify(entry);
        Assert.Equal(entry.Id, verified.Id); Assert.Equal(entry.TicketCode, verified.TicketCode);
        Assert.Equal("51F12345", verified.PlateNumber); Assert.Equal("VERIFIED", verified.VerificationStatus);
        Assert.Equal("OCCUPIED", (await f.Db.ParkingSlots.FindAsync(1L))!.PhysicalStatus);
        Assert.Equal(entry.Revision + 1, verified.Revision);
        Assert.Contains(await f.Db.AuditLogs.ToListAsync(), a => a.Action == "VERIFY_VEHICLE_AT_SLOT");
    }
    [Fact]
    public async Task Official_plate_conflict_does_not_merge_or_delete_waiting_session()
    {
        await using var f = await Create(); var a = await f.Enter(); var b = await f.Enter("VE-002");
        await f.Verify(a);
        Assert.Equal("PLATE_CONFLICT", (await Assert.ThrowsAsync<ParkingOperationException>(() => f.Verify(b, 2, "51F12345"))).Code);
        Assert.Equal(2, await f.Db.ParkingSessions.CountAsync());
        Assert.Equal("WAITING_VERIFICATION", (await f.Db.ParkingSessions.FindAsync(b.Id))!.VerificationStatus);
    }
    [Theory]
    [InlineData(3, "PETROL", "INCOMPATIBLE_FUEL")]
    [InlineData(4, "PETROL", "INVALID_OPERATION")]
    public async Task Incompatible_or_fixed_slot_is_rejected(long slot, string fuel, string code)
    {
        await using var f = await Create(); var entry = await f.Enter();
        Assert.Equal(code, (await Assert.ThrowsAsync<ParkingOperationException>(() => f.Verify(entry, slot, fuel: fuel))).Code);
    }
    [Theory]
    [InlineData("MAINTENANCE")]
    [InlineData("OCCUPIED")]
    [InlineData("UNAVAILABLE")]
    public async Task Unusable_slot_is_rejected(string state)
    {
        await using var f = await Create(); var entry = await f.Enter();
        (await f.Db.ParkingSlots.FindAsync(1L))!.PhysicalStatus = state; await f.Db.SaveChangesAsync();
        Assert.Equal("SLOT_UNAVAILABLE", (await Assert.ThrowsAsync<ParkingOperationException>(() => f.Verify(entry))).Code);
    }
    [Fact]
    public async Task Closed_floor_and_wrong_duty_are_rejected()
    {
        await using var f = await Create(); var entry = await f.Enter();
        var ex = await Assert.ThrowsAsync<ParkingOperationException>(() => f.Service.Verify(1, entry.Id, new(1, "51F12345", "PETROL", entry.Revision, true, "test"), 2, "STAFF", default));
        Assert.Equal(403, ex.Status);
        (await f.Db.ParkingFloors.FindAsync(1))!.IsActive = false; await f.Db.SaveChangesAsync();
        Assert.Equal("SLOT_UNAVAILABLE", (await Assert.ThrowsAsync<ParkingOperationException>(() => f.Verify(entry))).Code);
    }
    [Fact]
    public async Task A_second_vehicle_cannot_take_an_occupied_slot()
    {
        await using var f = await Create(); var a = await f.Enter(); var b = await f.Enter("VE-002"); await f.Verify(a);
        Assert.Equal("SLOT_UNAVAILABLE", (await Assert.ThrowsAsync<ParkingOperationException>(() => f.Verify(b, plate: "51F99999"))).Code);
    }
    [Fact]
    public async Task Move_requires_evidence_and_keeps_history_and_revision()
    {
        await using var f = await Create(); var verified = await f.Verify(await f.Enter());
        await Assert.ThrowsAsync<ParkingOperationException>(() => f.Service.Move(1, verified.Id, new(2, verified.Revision, false, true, "Chuyển xe"), 1, "ADMIN", default));
        var moved = await f.Service.Move(1, verified.Id, new(2, verified.Revision, true, true, "Chuyển xe theo yêu cầu"), 1, "ADMIN", default);
        Assert.Equal(verified.Id, moved.Id); Assert.Equal(2L, moved.CurrentSlotId);
        Assert.Equal("EMPTY", (await f.Db.ParkingSlots.FindAsync(1L))!.PhysicalStatus);
        Assert.Equal("OCCUPIED", (await f.Db.ParkingSlots.FindAsync(2L))!.PhysicalStatus);
        Assert.Equal(2, await f.Db.ParkingDispatches.CountAsync());
        Assert.Equal("STALE_DATA", (await Assert.ThrowsAsync<ParkingOperationException>(() => f.Service.Move(1, verified.Id, new(1, verified.Revision, true, true, "Lặp thao tác"), 1, "ADMIN", default))).Code);
    }
    [Fact]
    public async Task Lost_ticket_lookup_retains_status_and_replacement_does_not_create_another_session()
    {
        await using var f = await Create(); var entry = await f.Enter();
        await f.Service.ChangeTicket(1, "VE-001", new("LOST", null, "Khách báo mất"), 2, "STAFF", default);
        Assert.Equal("LOST", (await f.Service.Lookup(1, "VE-001", 1, "ADMIN", default)).TicketStatus);
        await Assert.ThrowsAsync<ParkingOperationException>(() => f.Service.ChangeTicket(1, "VE-001", new("REPLACE", "VE-NEW", "Đối chiếu"), 2, "STAFF", default));
        await f.Service.ChangeTicket(1, "VE-001", new("REPLACE", "VE-NEW", "Quản lý đối chiếu"), 1, "ADMIN", default);
        Assert.Equal(entry.Id, (await f.Service.Lookup(1, "VE-NEW", 1, "ADMIN", default)).Id);
        Assert.Single(await f.Db.ParkingSessions.ToListAsync());
        Assert.Single(await f.Db.ParkingTickets.Where(t => t.Status == "ISSUED").ToListAsync());
        Assert.Equal("REPLACED", (await f.Service.Lookup(1, "VE-001", 1, "ADMIN", default)).TicketStatus);
        Assert.Equal(404, (await Assert.ThrowsAsync<ParkingOperationException>(() => f.Service.Lookup(2, "VE-NEW", 1, "ADMIN", default))).Status);
    }
    [Fact]
    public async Task Recognition_must_belong_to_actor_and_match_saved_image_bytes()
    {
        await using var f = await Create();
        f.Ai.RecognitionRecords.Add(new AiRecognitionRecord { Id = 1, StaffUserId = 1, PlateNumber = "51F12345", ContentSha256 = "correct" });
        f.Db.StoredImages.Add(new StoredImage { Id = 1, UserId = 1, ContentSha256 = "wrong", PublicId = "img", Url = "https://example.test/1" });
        await f.Ai.SaveChangesAsync(); await f.Db.SaveChangesAsync();
        Assert.Equal("EVIDENCE_MISMATCH", (await Assert.ThrowsAsync<ParkingOperationException>(() => f.Enter(recognition: 1, image: 1))).Code);
        Assert.Empty(await f.Db.ParkingSessions.ToListAsync());
    }
    [Fact]
    public async Task Policy_drafts_do_not_apply_and_old_session_keeps_its_version()
    {
        await using var f = await Create(); var entry = await f.Enter();
        var request = new PolicyRequest(null, DateTime.UtcNow.AddMinutes(5), false, "Đề xuất chưa duyệt", null, null, null, null, null, null, null, null, null, null, null);
        var draft = Assert.IsType<ParkingPolicy>(await f.Service.CreatePolicy(1, request, 1, "ADMIN", default));
        Assert.Null(draft.ApprovedAt);
        Assert.Equal(1, (await ParkingPolicyResolver.ResolveAsync(f.Db, 1, 1, DateTime.UtcNow.AddHours(1)))!.Id);
        await Assert.ThrowsAsync<ParkingOperationException>(() => f.Service.CreatePolicy(1, request with { Approve = true }, 1, "ADMIN", default));
        await Assert.ThrowsAsync<ParkingOperationException>(() => f.Service.CreatePolicy(1, request with { HoldMinutes = -1 }, 1, "ADMIN", default));
        await Assert.ThrowsAsync<ParkingOperationException>(() => f.Service.CreatePolicy(1, request, 2, "ADMIN", default));
        var approved = Assert.IsType<ParkingPolicy>(await f.Service.CreatePolicy(1, request with { Approve = true, DecisionReference = "Quyết định kiểm thử", HoldMinutes = 7,
            EarlyArrivalMinutes = 0, LateArrivalMinutes = 3, CancellationNoticeMinutes = 0, DepositAmount = 0, NearFullThreshold = 1,
            FeeUnitMinutes = 60, FeeRounding = "CEILING", NoShowAction = "EXPIRE", CancellationAction = "ALLOW", RateSelection = "CHECK_IN" }, 1, "ADMIN", default));
        Assert.Equal(approved.Id, (await ParkingPolicyResolver.ResolveAsync(f.Db, 1, 1, DateTime.UtcNow.AddHours(1)))!.Id);
        Assert.Equal(1L, (await f.Db.ParkingSessions.FindAsync(entry.Id))!.PolicyId);
    }
    [Fact]
    public async Task Closing_zone_preserves_sessions_and_disallows_further_admission()
    {
        await using var f = await Create(); var entry = await f.Enter();
        await f.Service.SaveZone(1, 1, new("VL", "Vãng lai", "TRANSIENT", "INSIDE", false), 1, "ADMIN", default);
        await Assert.ThrowsAsync<ParkingOperationException>(() => f.Enter("VE-002"));
        Assert.Equal(entry.Id, (await f.Db.ParkingSessions.SingleAsync()).Id);
        await f.Verify(entry); // closing intake must still let an admitted vehicle finish verification
        await Assert.ThrowsAsync<ParkingOperationException>(() => f.Service.AssignFloor(1, 1, 2, 1, "ADMIN", default));
    }
    [Fact]
    public async Task Legacy_verification_cannot_bypass_official_plate_and_fuel_check()
    {
        await using var f = await Create(); var entry = await f.Enter();
        var legacy = new ParkingSessionService(f.Db, new ParkingAuthorizationService(f.Db), NullLogger<ParkingSessionService>.Instance);
        var result = await legacy.VerifySessionSlotAsync(entry.Id, new VerifySessionSlotRequestDto { SlotId = 1 }, 1, "ADMIN", null);
        Assert.False(result.Success);
        Assert.Equal("EMPTY", (await f.Db.ParkingSlots.FindAsync(1L))!.PhysicalStatus);
    }
    [Fact]
    public async Task Booking_cancellation_uses_the_attached_policy_instead_of_a_new_version()
    {
        await using var f = await Create();
        (await f.Db.ParkingPolicies.FindAsync(1L))!.CancellationAction = "DENY";
        f.Db.ParkingPolicies.Add(new ParkingPolicy { Id = 2, LotId = 1, EffectiveFrom = DateTime.UtcNow.AddMinutes(-1),
            ApprovedAt = DateTime.UtcNow, CreatedBy = 1, ApprovedBy = 1, CancellationAction = "ALLOW", CancellationNoticeMinutes = 0 });
        f.Db.Vehicles.Add(new Vehicle { Id = 1, UserId = 1, PlateNumber = "51F12345" });
        f.Db.Bookings.Add(new Booking { Id = 1, LotId = 1, UserId = 1, VehicleId = 1, PlateNumber = "51F12345", StartTime = DateTime.UtcNow.AddHours(2), PolicyId = 1 });
        await f.Db.SaveChangesAsync();
        var bookingService = new BookingService(f.Db, new ParkingAuthorizationService(f.Db), NullLogger<BookingService>.Instance);
        var result = await bookingService.CancelBookingAsync(1, new SmartParking.Application.DTOs.Bookings.CancelBookingRequestDto { Reason = "Test" }, 1, "ADMIN", null);
        Assert.False(result.Success); Assert.Equal(409, result.StatusCode);
        Assert.Equal("PENDING", (await f.Db.Bookings.FindAsync(1L))!.Status);
        Assert.Equal(2, (await ParkingPolicyResolver.ResolveAsync(f.Db, 1, null, DateTime.UtcNow))!.Id);
    }
    [Fact]
    public async Task Queue_reads_durable_tickets_and_excludes_verified_vehicles_when_waiting_only()
    {
        await using var f = await Create(); var a = await f.Enter(); var b = await f.Enter("VE-002");
        await f.Verify(a);
        var waiting = Assert.IsType<List<OperationSessionDto>>(await f.Service.Sessions(1, true, 1, "ADMIN", default));
        Assert.Equal(b.Id, Assert.Single(waiting).Id); Assert.Equal("VE-002", waiting[0].TicketCode);
        var all = Assert.IsType<List<OperationSessionDto>>(await f.Service.Sessions(1, false, 1, "ADMIN", default));
        Assert.Equal("A1", all.Single(s => s.Id == a.Id).SlotCode);
        await Assert.ThrowsAsync<ParkingOperationException>(() => f.Service.Sessions(2, false, 2, "STAFF", default));
    }
}
