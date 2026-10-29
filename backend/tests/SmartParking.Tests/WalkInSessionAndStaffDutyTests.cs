using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.Application.DTOs.Sessions;
using SmartParking.Domain.Constants;
using SmartParking.Domain.Entities;
using SmartParking.Infrastructure.Services;
using Xunit;

namespace SmartParking.Tests;

public class WalkInSessionAndStaffDutyTests
{
    private async Task<(ParkingSessionService SessionService, StaffLotAssignmentService AssignmentService, TestSmartParkingDbContext Context)> CreateServicesAsync()
    {
        var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var adminUser = new User
        {
            Id = 1,
            Username = "admin_duty_test",
            Email = "admin@dutytest.com",
            FullName = "Admin Duty Test",
            PasswordHash = "dummy",
            RoleId = 1,
            IsActive = true,
            ApprovalStatus = "APPROVED"
        };
        context.Users.Add(adminUser);
        await context.SaveChangesAsync();

        var authService = new ParkingAuthorizationService(context);
        var sessionService = new ParkingSessionService(context, authService, NullLogger<ParkingSessionService>.Instance);
        var assignmentService = new StaffLotAssignmentService(context, NullLogger<StaffLotAssignmentService>.Instance);
        return (sessionService, assignmentService, context);
    }

    private static (ParkingLot Lot, ParkingFloor Floor, List<ParkingSlot> Slots) SeedLot(TestSmartParkingDbContext context, int lotId, string lotName, int slotCount = 5)
    {
        var lot = new ParkingLot
        {
            Id = lotId,
            Name = lotName,
            Address = "123 Test St",
            TotalCapacity = slotCount,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.ParkingLots.Add(lot);

        var floor = new ParkingFloor
        {
            Id = lotId * 10 + 1,
            LotId = lotId,
            Lot = lot,
            FloorNumber = 1,
            Name = "Tầng 1",
            TotalSlots = slotCount,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.ParkingFloors.Add(floor);

        var slots = new List<ParkingSlot>();
        for (int i = 1; i <= slotCount; i++)
        {
            var slot = new ParkingSlot
            {
                Id = lotId * 100 + i,
                FloorId = floor.Id,
                Floor = floor,
                SlotCode = $"P{lotId}-{i:D2}",
                PhysicalStatus = SlotPhysicalStatuses.Empty,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            slots.Add(slot);
            context.ParkingSlots.Add(slot);
        }

        context.SaveChanges();
        return (lot, floor, slots);
    }

    private static void AddStaffWithDuty(TestSmartParkingDbContext context, long userId, int lotId, string duty)
    {
        var user = new User
        {
            Id = userId,
            Username = $"staff_duty_{userId}",
            Email = $"staff_{userId}@test.com",
            FullName = $"Staff {userId}",
            PasswordHash = "dummy",
            RoleId = 3, // Staff
            IsActive = true,
            ApprovalStatus = "APPROVED"
        };
        context.Users.Add(user);

        var assignment = new StaffLotAssignment
        {
            UserId = userId,
            LotId = lotId,
            Duty = duty,
            AssignedBy = 1,
            IsActive = true,
            AssignedAt = DateTime.UtcNow
        };
        context.StaffLotAssignments.Add(assignment);
    }

    [Fact]
    public async Task StaffDuty_GateOnly_CanCheckInAndCheckOut_CannotVerifySlot()
    {
        var (sessionService, _, context) = await CreateServicesAsync();
        SeedLot(context, 201, "Bãi Gate Staff");
        AddStaffWithDuty(context, 2011, 201, StaffDuties.Gate);
        await context.SaveChangesAsync();

        // 1. GATE staff check-in succeeds
        var checkInRes = await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 201, PlateNumber = "51A-GATE-01" },
            actorId: 2011,
            actorRole: Roles.Staff,
            ipAddress: "127.0.0.1");

        Assert.True(checkInRes.Success);
        Assert.NotNull(checkInRes.Data);
        var sessionId = checkInRes.Data!.Id;

        // 2. GATE staff attempts VerifySessionSlot -> rejected with duty error
        var verifyRes = await sessionService.VerifySessionSlotAsync(
            sessionId,
            new VerifySessionSlotRequestDto { SlotId = 20101 },
            actorId: 2011,
            actorRole: Roles.Staff,
            ipAddress: "127.0.0.1");

        Assert.False(verifyRes.Success);
        Assert.Contains("SLOT_VERIFIER", verifyRes.Message);

        // 3. GATE staff check-out requires financial clearance (PAID)
        var session = await context.ParkingSessions.FindAsync(sessionId);
        session!.Status = SessionStatuses.Paid;
        session.PaidAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var checkOutRes = await sessionService.CheckOutAsync(
            sessionId,
            new CheckOutRequestDto(),
            actorId: 2011,
            actorRole: Roles.Staff,
            ipAddress: "127.0.0.1");

        Assert.True(checkOutRes.Success);
        Assert.Equal(SessionStatuses.Completed, checkOutRes.Data!.Status);
    }

    [Fact]
    public async Task StaffDuty_SlotVerifierOnly_CanVerifySlot_CannotCheckInOrCheckOut()
    {
        var (sessionService, _, context) = await CreateServicesAsync();
        SeedLot(context, 202, "Bãi Verifier Staff");
        AddStaffWithDuty(context, 2021, 202, StaffDuties.SlotVerifier);
        await context.SaveChangesAsync();

        // 1. SLOT_VERIFIER attempts check-in -> rejected
        var checkInRes = await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 202, PlateNumber = "51A-VERIFIER-01" },
            actorId: 2021,
            actorRole: Roles.Staff,
            ipAddress: "127.0.0.1");

        Assert.False(checkInRes.Success);
        Assert.Contains("GATE", checkInRes.Message);

        // Admin checks vehicle in
        var adminCheckIn = await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 202, PlateNumber = "51A-VERIFIER-01" },
            actorId: 1,
            actorRole: Roles.Admin,
            ipAddress: "127.0.0.1");
        Assert.True(adminCheckIn.Success);
        var sessionId = adminCheckIn.Data!.Id;

        // 2. SLOT_VERIFIER verifies slot -> succeeds
        var verifyRes = await sessionService.VerifySessionSlotAsync(
            sessionId,
            new VerifySessionSlotRequestDto { SlotId = 20201 },
            actorId: 2021,
            actorRole: Roles.Staff,
            ipAddress: "127.0.0.1");

        Assert.True(verifyRes.Success);
        Assert.Equal(20201, verifyRes.Data!.CurrentSlotId);

        // Verify physical status updated to OCCUPIED
        var slot = await context.ParkingSlots.FindAsync(20201L);
        Assert.Equal(SlotPhysicalStatuses.Occupied, slot!.PhysicalStatus);

        // 3. SLOT_VERIFIER attempts check-out -> rejected
        var checkOutRes = await sessionService.CheckOutAsync(
            sessionId,
            new CheckOutRequestDto(),
            actorId: 2021,
            actorRole: Roles.Staff,
            ipAddress: "127.0.0.1");

        Assert.False(checkOutRes.Success);
        Assert.Contains("GATE", checkOutRes.Message);
    }

    [Fact]
    public async Task StaffDuty_Both_CanPerformAllOperations()
    {
        var (sessionService, _, context) = await CreateServicesAsync();
        SeedLot(context, 203, "Bãi Both Duty Staff");
        AddStaffWithDuty(context, 2031, 203, StaffDuties.Both);
        await context.SaveChangesAsync();

        // Check-in
        var checkInRes = await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 203, PlateNumber = "51A-BOTH-01" },
            actorId: 2031,
            actorRole: Roles.Staff,
            ipAddress: "127.0.0.1");
        Assert.True(checkInRes.Success);
        var sessionId = checkInRes.Data!.Id;

        // Verify slot
        var verifyRes = await sessionService.VerifySessionSlotAsync(
            sessionId,
            new VerifySessionSlotRequestDto { SlotId = 20301 },
            actorId: 2031,
            actorRole: Roles.Staff,
            ipAddress: "127.0.0.1");
        Assert.True(verifyRes.Success);

        // Mark session PAID for financial clearance
        var session = await context.ParkingSessions.FindAsync(sessionId);
        session!.Status = SessionStatuses.Paid;
        session.PaidAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        // Check-out
        var checkOutRes = await sessionService.CheckOutAsync(
            sessionId,
            new CheckOutRequestDto(),
            actorId: 2031,
            actorRole: Roles.Staff,
            ipAddress: "127.0.0.1");
        Assert.True(checkOutRes.Success);
    }

    [Fact]
    public async Task CheckIn_WalkIn_WithoutFixedSlot_EntersUnmatchedQueue()
    {
        var (sessionService, _, context) = await CreateServicesAsync();
        SeedLot(context, 204, "Bãi Unmatched Queue");
        await context.SaveChangesAsync();

        // Check in walk-in car without preassigned slot (SlotId = null)
        var checkInRes = await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 204, PlateNumber = "51A-WALKIN-QUEUE" },
            actorId: 1,
            actorRole: Roles.Admin,
            ipAddress: "127.0.0.1");

        Assert.True(checkInRes.Success);
        Assert.Null(checkInRes.Data!.CurrentSlotId);
        Assert.Equal(SessionStatuses.Active, checkInRes.Data.Status);

        // Query unmatched sessions queue
        var unmatched = await sessionService.GetUnmatchedSessionsAsync(204, actorId: 1, actorRole: Roles.Admin);
        Assert.NotEmpty(unmatched);
        Assert.Contains(unmatched, s => s.PlateNumber == "51A-WALKIN-QUEUE" && s.SessionId == checkInRes.Data.Id);
    }

    [Fact]
    public async Task UnmatchedOccupiedSlots_IdentifiesSensorOccupancyWithoutSession()
    {
        var (sessionService, _, context) = await CreateServicesAsync();
        var (_, _, slots) = SeedLot(context, 205, "Bãi Sensor Unmatched");

        // Simulate IoT sensor reporting slot OCCUPIED
        slots[2].PhysicalStatus = SlotPhysicalStatuses.Occupied;
        await context.SaveChangesAsync();

        var unmatchedSlots = await sessionService.GetUnmatchedOccupiedSlotsAsync(205, actorId: 1, actorRole: Roles.Admin);
        Assert.Single(unmatchedSlots);
        Assert.Equal(slots[2].Id, unmatchedSlots[0].SlotId);
        Assert.Equal(slots[2].SlotCode, unmatchedSlots[0].SlotCode);
    }

    [Fact]
    public async Task VerifySessionSlot_BindsSessionAndSlot_PreventsDuplicateAssignment()
    {
        var (sessionService, _, context) = await CreateServicesAsync();
        SeedLot(context, 206, "Bãi Anti Duplicate");
        await context.SaveChangesAsync();

        // 2 walk-in vehicles
        var session1 = (await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 206, PlateNumber = "51A-FIRST-CAR" },
            1, Roles.Admin, "127.0.0.1")).Data!;

        var session2 = (await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 206, PlateNumber = "51A-SECOND-CAR" },
            1, Roles.Admin, "127.0.0.1")).Data!;

        // Staff binds Session 1 to slot 20601
        var verify1 = await sessionService.VerifySessionSlotAsync(
            session1.Id,
            new VerifySessionSlotRequestDto { SlotId = 20601 },
            1, Roles.Admin, "127.0.0.1");
        Assert.True(verify1.Success);

        // Staff attempts to bind Session 2 to the same slot 20601 -> rejected
        var verify2 = await sessionService.VerifySessionSlotAsync(
            session2.Id,
            new VerifySessionSlotRequestDto { SlotId = 20601 },
            1, Roles.Admin, "127.0.0.1");

        Assert.False(verify2.Success);
        Assert.Contains("Không thể gán trùng lặp", verify2.Message);
    }

    [Fact]
    public async Task ReportSessionWrongSlot_MovesToActualSlot_PreservesPreviousPhysicalState()
    {
        var (sessionService, _, context) = await CreateServicesAsync();
        SeedLot(context, 207, "Bãi Wrong Slot Correction");
        await context.SaveChangesAsync();

        var session = (await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 207, PlateNumber = "51A-LOST-CAR" },
            1, Roles.Admin, "127.0.0.1")).Data!;

        // Initially verified at slot 20701
        await sessionService.VerifySessionSlotAsync(
            session.Id,
            new VerifySessionSlotRequestDto { SlotId = 20701 },
            1, Roles.Admin, "127.0.0.1");

        var slot1 = await context.ParkingSlots.FindAsync(20701L);
        Assert.Equal(SlotPhysicalStatuses.Occupied, slot1!.PhysicalStatus);

        // Staff detects car actually parked in slot 20702
        var reportRes = await sessionService.ReportSessionWrongSlotAsync(
            session.Id,
            new ReportWrongSlotSessionRequestDto { ActualSlotId = 20702, Reason = "Tài xế nhầm ô bên cạnh" },
            1, Roles.Admin, "127.0.0.1");

        Assert.True(reportRes.Success);
        Assert.Equal(20702, reportRes.Data!.CurrentSlotId);

        // Invariant: Old slot physical status is preserved (NOT blindly set to EMPTY without sensor/physical clearance)
        slot1 = await context.ParkingSlots.FindAsync(20701L);
        Assert.Equal(SlotPhysicalStatuses.Occupied, slot1!.PhysicalStatus);

        // New slot marked OCCUPIED
        var slot2 = await context.ParkingSlots.FindAsync(20702L);
        Assert.Equal(SlotPhysicalStatuses.Occupied, slot2!.PhysicalStatus);
    }

    [Fact]
    public async Task CheckOut_FinancialGuard_RequiresPaymentOrAdminManagerExemption()
    {
        var (sessionService, _, context) = await CreateServicesAsync();
        SeedLot(context, 208, "Bãi Financial Guard");
        AddStaffWithDuty(context, 2081, 208, StaffDuties.Gate);
        await context.SaveChangesAsync();

        var checkInRes = await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 208, PlateNumber = "51A-UNPAID-CAR" },
            1, Roles.Admin, "127.0.0.1");

        var session = await context.ParkingSessions.FindAsync(checkInRes.Data!.Id);
        // Default check-in session has TotalFee = 0, Status = ACTIVE (unsettled)
        Assert.Equal(0m, session!.TotalFee);
        Assert.Equal(SessionStatuses.Active, session.Status);

        // 1. TotalFee = 0 but still ACTIVE: Regular Gate Staff checkout -> rejected (unsettled)
        var failExit = await sessionService.CheckOutAsync(
            session.Id,
            new CheckOutRequestDto(),
            actorId: 2081,
            actorRole: Roles.Staff,
            ipAddress: "127.0.0.1");

        Assert.False(failExit.Success);
        Assert.Contains("chưa hoàn tất tính phí và thanh toán", failExit.Message);

        // 1b. Session with fee > 0 (TotalFee = 35,000đ): Regular Gate Staff checkout -> rejected (fee-bearing checkout blocked)
        session.TotalFee = 35000m;
        await context.SaveChangesAsync();

        var failFeeExit = await sessionService.CheckOutAsync(
            session.Id,
            new CheckOutRequestDto(),
            actorId: 2081,
            actorRole: Roles.Staff,
            ipAddress: "127.0.0.1");

        Assert.False(failFeeExit.Success);
        Assert.Contains("Check-out có thu phí đang bị khóa cho tới khi thanh toán hoàn tất", failFeeExit.Message);

        // 2. Gate Staff attempting self-exemption with AllowExitWithoutPayment -> strictly rejected
        var staffExemptFail = await sessionService.CheckOutAsync(
            session.Id,
            new CheckOutRequestDto
            {
                AllowExitWithoutPayment = true,
                ExemptionReason = "Staff tự cho xe ra"
            },
            actorId: 2081,
            actorRole: Roles.Staff,
            ipAddress: "127.0.0.1");

        Assert.False(staffExemptFail.Success);
        Assert.Contains("Nhân viên cổng (STAFF) không có thẩm quyền", staffExemptFail.Message);

        // 3. ADMIN authorized exemption checkout -> succeeds
        var exemptExit = await sessionService.CheckOutAsync(
            session.Id,
            new CheckOutRequestDto
            {
                AllowExitWithoutPayment = true,
                ExemptionReason = "Xe ưu tiên / Miễn phí sự kiện"
            },
            actorId: 1,
            actorRole: Roles.Admin,
            ipAddress: "127.0.0.1");

        Assert.True(exemptExit.Success);
        Assert.Equal(SessionStatuses.Completed, exemptExit.Data!.Status);

        // 4. Retry checkout on already completed session -> rejected (cannot complete twice)
        var retryExit = await sessionService.CheckOutAsync(
            session.Id,
            new CheckOutRequestDto(),
            actorId: 1,
            actorRole: Roles.Admin,
            ipAddress: "127.0.0.1");

        Assert.False(retryExit.Success);
        Assert.Contains("đã kết thúc trước đó", retryExit.Message);
    }

    [Fact]
    public async Task CheckIn_AntiPassback_DuplicateActivePlateRejected()
    {
        var (sessionService, _, context) = await CreateServicesAsync();
        SeedLot(context, 209, "Bãi Anti Passback");
        await context.SaveChangesAsync();

        var first = await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 209, PlateNumber = "51A-PASSBACK" },
            1, Roles.Admin, "127.0.0.1");
        Assert.True(first.Success);

        // Second check-in while first is still active -> rejected
        var second = await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 209, PlateNumber = "51A-PASSBACK" },
            1, Roles.Admin, "127.0.0.1");

        Assert.False(second.Success);
        Assert.Contains("đang có phiên đỗ xe chưa kết thúc", second.Message);
    }

    [Fact]
    public async Task VerifySessionSlot_ConcurrentAttempts_OnlyOneSucceeds()
    {
        var (sessionService, _, context) = await CreateServicesAsync();
        SeedLot(context, 210, "Bãi Concurrency Verify");
        await context.SaveChangesAsync();

        var session1 = (await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 210, PlateNumber = "51A-RACE-01" },
            1, Roles.Admin, "127.0.0.1")).Data!;

        var session2 = (await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 210, PlateNumber = "51A-RACE-02" },
            1, Roles.Admin, "127.0.0.1")).Data!;

        var task1 = sessionService.VerifySessionSlotAsync(
            session1.Id,
            new VerifySessionSlotRequestDto { SlotId = 21001 },
            1, Roles.Admin, "127.0.0.1");

        var task2 = sessionService.VerifySessionSlotAsync(
            session2.Id,
            new VerifySessionSlotRequestDto { SlotId = 21001 },
            1, Roles.Admin, "127.0.0.1");

        var results = await Task.WhenAll(task1, task2);

        Assert.Single(results, r => r.Success);
        Assert.Single(results, r => !r.Success);
    }

    [Fact]
    public async Task StaffDuty_CreateAssignment_DefaultDutyIsGate_AndInvalidDutyRejected()
    {
        var (_, assignmentService, context) = await CreateServicesAsync();
        SeedLot(context, 211, "Bãi Duty Validation");

        var staff1 = new User
        {
            Id = 2111,
            Username = "staff_val_2111",
            Email = "staff_val@test.com",
            FullName = "Staff Val",
            PasswordHash = "dummy",
            RoleId = 3,
            IsActive = true,
            ApprovalStatus = "APPROVED"
        };

        var staff2 = new User
        {
            Id = 2112,
            Username = "staff_val_2112",
            Email = "staff_val2@test.com",
            FullName = "Staff Val 2",
            PasswordHash = "dummy",
            RoleId = 3,
            IsActive = true,
            ApprovalStatus = "APPROVED"
        };
        context.Users.AddRange(staff1, staff2);
        await context.SaveChangesAsync();

        // 1. Unspecified duty -> defaults to GATE (least privilege, NOT BOTH)
        var defaultDutyRes = await assignmentService.CreateAssignmentAsync(
            new SmartParking.Application.DTOs.StaffAssignments.CreateStaffAssignmentRequestDto
            {
                UserId = 2111,
                LotId = 211,
                Duty = ""
            },
            actorId: 1,
            ipAddress: "127.0.0.1");

        Assert.True(defaultDutyRes.Success);
        Assert.Equal(StaffDuties.Gate, defaultDutyRes.Data!.Duty);

        // 2. Invalid duty -> rejected
        var invalidDutyRes = await assignmentService.CreateAssignmentAsync(
            new SmartParking.Application.DTOs.StaffAssignments.CreateStaffAssignmentRequestDto
            {
                UserId = 2112,
                LotId = 211,
                Duty = "SUPER_STAFF"
            },
            actorId: 1,
            ipAddress: "127.0.0.1");

        Assert.False(invalidDutyRes.Success);
        Assert.Contains("không hợp lệ", invalidDutyRes.Message);
    }

    [Fact]
    public async Task PaymentClearance_ValidPayment_EnablesGateCheckOut()
    {
        var (sessionService, _, context) = await CreateServicesAsync();
        SeedLot(context, 212, "Bãi Payment Clearance Contract");
        AddStaffWithDuty(context, 2121, 212, StaffDuties.Gate);
        await context.SaveChangesAsync();

        // 1. Vehicle checks in at gate
        var checkInRes = await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 212, PlateNumber = "51A-CLEARANCE-01" },
            actorId: 2121,
            actorRole: Roles.Staff,
            ipAddress: "127.0.0.1");
        Assert.True(checkInRes.Success);
        var sessionId = checkInRes.Data!.Id;

        // 2. Gate staff attempts checkout before payment -> rejected
        var prePaymentCheckOut = await sessionService.CheckOutAsync(
            sessionId,
            new CheckOutRequestDto(),
            actorId: 2121,
            actorRole: Roles.Staff,
            ipAddress: "127.0.0.1");
        Assert.False(prePaymentCheckOut.Success);
        Assert.Contains("chưa hoàn tất tính phí và thanh toán", prePaymentCheckOut.Message);

        // 3. Nam Anh's Payment module calls RecordPaymentClearanceAsync
        var clearanceRes = await sessionService.RecordPaymentClearanceAsync(
            sessionId,
            amountPaid: 35000m,
            transactionRef: "VNPAY-20261029-TX999",
            paymentMethod: "VNPAY_QR",
            actorId: 1,
            actorRole: Roles.Admin,
            ipAddress: "127.0.0.1");

        Assert.True(clearanceRes.Success);
        Assert.Equal(SessionStatuses.Paid, clearanceRes.Data!.Status);
        Assert.Equal(35000m, clearanceRes.Data.TotalFee);
        Assert.NotNull(clearanceRes.Data.PaidAt);

        // 4. Gate staff checks vehicle out now that financial clearance is complete -> succeeds
        var postPaymentCheckOut = await sessionService.CheckOutAsync(
            sessionId,
            new CheckOutRequestDto(),
            actorId: 2121,
            actorRole: Roles.Staff,
            ipAddress: "127.0.0.1");

        Assert.True(postPaymentCheckOut.Success);
        Assert.Equal(SessionStatuses.Completed, postPaymentCheckOut.Data!.Status);

        // Verify audit trail for PAYMENT_CLEARED
        var audit = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            context.AuditLogs, a => a.Action == "PAYMENT_CLEARED" && a.TargetId == sessionId.ToString());
        Assert.NotNull(audit);
    }
}
