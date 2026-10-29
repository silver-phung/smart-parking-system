using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.Application.DTOs.Sessions;
using SmartParking.Domain.Constants;
using SmartParking.Domain.Entities;
using SmartParking.Infrastructure.Common;
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

        // Fix 4 Invariant: Sensor state remains unchanged (EMPTY) under business verification (staff association != IoT sensor update)
        var slot = await context.ParkingSlots.FindAsync(20201L);
        Assert.Equal(SlotPhysicalStatuses.Empty, slot!.PhysicalStatus);

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

        var slot1 = await context.ParkingSlots.FindAsync(20701L);
        slot1!.PhysicalStatus = SlotPhysicalStatuses.Occupied; // Simulated IoT sensor telemetry
        await context.SaveChangesAsync();

        // Initially verified at slot 20701
        await sessionService.VerifySessionSlotAsync(
            session.Id,
            new VerifySessionSlotRequestDto { SlotId = 20701 },
            1, Roles.Admin, "127.0.0.1");

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

        // New slot physical status remains what IoT reported (does not force OCCUPIED)
        var slot2 = await context.ParkingSlots.FindAsync(20702L);
        Assert.Equal(SlotPhysicalStatuses.Empty, slot2!.PhysicalStatus);
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

        // 3. Nam Anh's Payment module calls RecordPaymentClearanceAsync via trusted server-to-server flow
        var clearanceRes = await sessionService.RecordPaymentClearanceAsync(
            sessionId,
            amountPaid: 35000m,
            transactionRef: "VNPAY-20261029-TX999",
            paymentMethod: "VNPAY_QR",
            actorId: 1,
            actorRole: Roles.Admin,
            ipAddress: "127.0.0.1",
            isTrustedPaymentServerFlow: true);

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

    [Fact]
    public async Task PaymentClearance_UntrustedCaller_FailsClosedWithPaymentIntegrationBlocked()
    {
        var (sessionService, _, context) = await CreateServicesAsync();
        SeedLot(context, 212, "Bãi Untrusted Clearance");
        var checkInRes = await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 212, PlateNumber = "51A-UNTRUSTED-01" },
            actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1");
        var sessionId = checkInRes.Data!.Id;

        // Even with Admin role, untrusted caller without server-side verification must fail-closed
        var res = await sessionService.RecordPaymentClearanceAsync(
            sessionId, 20000m, "TX-SELF-DECLARED", "MOCK",
            actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1",
            isTrustedPaymentServerFlow: false);

        Assert.False(res.Success);
        Assert.Contains("PAYMENT INTEGRATION BLOCKED", res.Message);
    }

    [Fact]
    public async Task PaymentClearance_UnauthorizedRole_StaffOrUser_IsRejected()
    {
        var (sessionService, _, context) = await CreateServicesAsync();
        SeedLot(context, 213, "Bãi Auth Clearance");
        AddStaffWithDuty(context, 2131, 213, StaffDuties.Gate);
        await context.SaveChangesAsync();

        var checkInRes = await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 213, PlateNumber = "51A-UNAUTH-01" },
            actorId: 2131, actorRole: Roles.Staff, ipAddress: "127.0.0.1");
        var sessionId = checkInRes.Data!.Id;

        // Staff attempts payment clearance -> 403 Forbidden
        var staffClearance = await sessionService.RecordPaymentClearanceAsync(
            sessionId, 20000m, "TX-STAFF-ATTEMPT", "CASH",
            actorId: 2131, actorRole: Roles.Staff, ipAddress: "127.0.0.1",
            isTrustedPaymentServerFlow: true);
        Assert.False(staffClearance.Success);
        Assert.Contains("Dịch vụ Thanh toán", staffClearance.Message);

        // User attempts payment clearance -> 403 Forbidden
        var userClearance = await sessionService.RecordPaymentClearanceAsync(
            sessionId, 20000m, "TX-USER-ATTEMPT", "MOCK",
            actorId: 999, actorRole: Roles.User, ipAddress: "127.0.0.1",
            isTrustedPaymentServerFlow: true);
        Assert.False(userClearance.Success);
        Assert.Contains("Dịch vụ Thanh toán", userClearance.Message);
    }

    [Fact]
    public async Task PaymentClearance_AmountZeroOrNegative_IsRejected()
    {
        var (sessionService, _, context) = await CreateServicesAsync();
        SeedLot(context, 214, "Bãi Zero Amount");
        var checkInRes = await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 214, PlateNumber = "51A-ZERO-01" },
            actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1");
        var sessionId = checkInRes.Data!.Id;

        // Amount <= 0 rejected
        var zeroClearance = await sessionService.RecordPaymentClearanceAsync(
            sessionId, 0m, "TX-ZERO", "MOCK",
            actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1",
            isTrustedPaymentServerFlow: true);
        Assert.False(zeroClearance.Success);
        Assert.Contains("phải lớn hơn 0", zeroClearance.Message);

        var negClearance = await sessionService.RecordPaymentClearanceAsync(
            sessionId, -5000m, "TX-NEG", "MOCK",
            actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1",
            isTrustedPaymentServerFlow: true);
        Assert.False(negClearance.Success);
        Assert.Contains("phải lớn hơn 0", negClearance.Message);
    }

    [Fact]
    public async Task PaymentClearance_UnderpaymentOfTotalFee_IsRejected()
    {
        var (sessionService, _, context) = await CreateServicesAsync();
        SeedLot(context, 215, "Bãi Underpayment");
        var checkInRes = await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 215, PlateNumber = "51A-UNDER-01" },
            actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1");
        var sessionId = checkInRes.Data!.Id;

        // Simulate authoritative pricing calculation by Nam Anh set TotalFee = 50,000 VNĐ
        var session = await context.ParkingSessions.FindAsync(sessionId);
        session!.TotalFee = 50000m;
        await context.SaveChangesAsync();

        // Caller attempts to clear payment with only 30,000 VNĐ -> rejected
        var underpaid = await sessionService.RecordPaymentClearanceAsync(
            sessionId, 30000m, "TX-UNDERPAID-01", "VNPAY",
            actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1",
            isTrustedPaymentServerFlow: true);
        Assert.False(underpaid.Success);
        Assert.Contains("không đủ so với phí gửi xe cần thanh toán", underpaid.Message);
    }

    [Fact]
    public async Task PaymentClearance_DuplicateTransactionRef_AcrossDifferentSessions_IsRejected()
    {
        var (sessionService, _, context) = await CreateServicesAsync();
        SeedLot(context, 216, "Bãi Duplicate Ref");
        var s1Res = await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 216, PlateNumber = "51A-REF-01" },
            actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1");
        var s2Res = await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 216, PlateNumber = "51A-REF-02" },
            actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1");

        // Clear session 1 with ref TX-UNIQUE-100
        var c1 = await sessionService.RecordPaymentClearanceAsync(
            s1Res.Data!.Id, 25000m, "TX-UNIQUE-100", "MOMOPAY",
            actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1",
            isTrustedPaymentServerFlow: true);
        Assert.True(c1.Success);

        // Attempt to clear session 2 with SAME ref TX-UNIQUE-100 -> rejected
        var c2 = await sessionService.RecordPaymentClearanceAsync(
            s2Res.Data!.Id, 25000m, "TX-UNIQUE-100", "MOMOPAY",
            actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1",
            isTrustedPaymentServerFlow: true);
        Assert.False(c2.Success);
        Assert.Contains("đã được sử dụng cho một phiên gửi xe khác", c2.Message);
    }

    [Fact]
    public async Task PaymentClearance_IdempotencyAndMismatchedRetry_HandledCorrectly()
    {
        var (sessionService, _, context) = await CreateServicesAsync();
        SeedLot(context, 217, "Bãi Idempotency");
        var sRes = await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 217, PlateNumber = "51A-IDEMP-01" },
            actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1");
        var sessionId = sRes.Data!.Id;

        // First clearance
        var c1 = await sessionService.RecordPaymentClearanceAsync(
            sessionId, 40000m, "TX-IDEMPOTENT-888", "ZALOPAY",
            actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1",
            isTrustedPaymentServerFlow: true);
        Assert.True(c1.Success);

        // Idempotent retry with identical ref -> returns true
        var retry = await sessionService.RecordPaymentClearanceAsync(
            sessionId, 40000m, "TX-IDEMPOTENT-888", "ZALOPAY",
            actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1",
            isTrustedPaymentServerFlow: true);
        Assert.True(retry.Success);
        Assert.Contains("đã được ghi nhận thanh toán hợp lệ trước đó", retry.Message);

        // Retry on already PAID session with DIFFERENT ref -> rejected
        var diffRef = await sessionService.RecordPaymentClearanceAsync(
            sessionId, 40000m, "TX-ANOTHER-999", "ZALOPAY",
            actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1",
            isTrustedPaymentServerFlow: true);
        Assert.False(diffRef.Success);
        Assert.Contains("đã hoàn tất thanh toán trước đó với một giao dịch khác", diffRef.Message);
    }

    [Fact]
    public async Task CheckIn_AssignDuplicateSlot_AlreadyOccupiedByAnotherActiveSession_IsRejected()
    {
        var (sessionService, _, context) = await CreateServicesAsync();
        var (_, _, slots) = SeedLot(context, 218, "Bãi Duplicate Slot", slotCount: 3);
        var slotToOccupy = slots[0];

        // Session 1 checks in and is assigned slotToOccupy
        var s1Res = await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 218, PlateNumber = "51A-OCCUPIER", SlotId = slotToOccupy.Id },
            actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1");
        Assert.True(s1Res.Success);
        Assert.Equal(slotToOccupy.Id, s1Res.Data!.CurrentSlotId);

        // Session 2 attempts check in with same slot -> rejected
        var s2Res = await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 218, PlateNumber = "51A-INTRUDER", SlotId = slotToOccupy.Id },
            actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1");
        Assert.False(s2Res.Success);
        Assert.Contains("hiện đang được sử dụng bởi một phiên đỗ xe khác", s2Res.Message);

        // Normal walk-in with SlotId = null -> succeeds
        var normalWalkIn = await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 218, PlateNumber = "51A-NORMAL-WALKIN", SlotId = null },
            actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1");
        Assert.True(normalWalkIn.Success);
        Assert.Null(normalWalkIn.Data!.CurrentSlotId);
    }

    [Fact]
    public async Task CheckIn_AssignSlotUnderMaintenance_IsRejected()
    {
        var (sessionService, _, context) = await CreateServicesAsync();
        var (_, _, slots) = SeedLot(context, 219, "Bãi Maint Slot", slotCount: 3);
        var maintSlot = slots[1];
        maintSlot.PhysicalStatus = SlotPhysicalStatuses.Maintenance;
        await context.SaveChangesAsync();

        var res = await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 219, PlateNumber = "51A-MAINT-TEST", SlotId = maintSlot.Id },
            actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1");
        Assert.False(res.Success);
        Assert.Contains("bảo trì hoặc ngừng phục vụ", res.Message);
    }

    [Fact]
    public async Task VerifySessionSlot_MaintenanceSlot_IsRejected_AndSensorPreserved()
    {
        var (sessionService, _, context) = await CreateServicesAsync();
        var (_, _, slots) = SeedLot(context, 220, "Bãi Verify Maint", slotCount: 3);
        var maintSlot = slots[1];
        maintSlot.PhysicalStatus = SlotPhysicalStatuses.Maintenance;
        await context.SaveChangesAsync();

        var checkIn = await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 220, PlateNumber = "51A-VERIFY-MAINT" },
            actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1");
        var sessionId = checkIn.Data!.Id;

        // Attempt to verify to maintenance slot -> rejected
        var verifyMaint = await sessionService.VerifySessionSlotAsync(
            sessionId,
            new VerifySessionSlotRequestDto { SlotId = maintSlot.Id },
            actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1");
        Assert.False(verifyMaint.Success);
        Assert.Contains("bảo trì hoặc không khả dụng", verifyMaint.Message);

        // Verify to normal slot -> succeeds and sensor state remains EMPTY
        var normalSlot = slots[0];
        var verifyNormal = await sessionService.VerifySessionSlotAsync(
            sessionId,
            new VerifySessionSlotRequestDto { SlotId = normalSlot.Id },
            actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1");
        Assert.True(verifyNormal.Success);
        var reloadedSlot = await context.ParkingSlots.FindAsync(normalSlot.Id);
        Assert.Equal(SlotPhysicalStatuses.Empty, reloadedSlot!.PhysicalStatus);
    }

    [Fact]
    public async Task CheckOut_ConfirmedPlateMismatch_IsRejected_AndNormalizedMatch_Succeeds()
    {
        var (sessionService, _, context) = await CreateServicesAsync();
        SeedLot(context, 221, "Bãi Plate Verify");
        AddStaffWithDuty(context, 2211, 221, StaffDuties.Gate);
        await context.SaveChangesAsync();

        var checkIn = await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 221, PlateNumber = "51A-123.45" },
            actorId: 2211, actorRole: Roles.Staff, ipAddress: "127.0.0.1");
        var sessionId = checkIn.Data!.Id;

        // Clear payment so checkout financial precondition passes
        await sessionService.RecordPaymentClearanceAsync(
            sessionId, 20000m, "TX-PLATE-01", "CASH",
            actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1",
            isTrustedPaymentServerFlow: true);

        // Attempt checkout with wrong plate
        var wrongPlateRes = await sessionService.CheckOutAsync(
            sessionId,
            new CheckOutRequestDto { ConfirmedPlateNumber = "29B-999.99" },
            actorId: 2211, actorRole: Roles.Staff, ipAddress: "127.0.0.1");
        Assert.False(wrongPlateRes.Success);
        Assert.Contains("không khớp", wrongPlateRes.Message);

        // Checkout with matching plate normalized (e.g. 51A12345 without punctuation) -> succeeds
        var matchPlateRes = await sessionService.CheckOutAsync(
            sessionId,
            new CheckOutRequestDto { ConfirmedPlateNumber = "51a12345" },
            actorId: 2211, actorRole: Roles.Staff, ipAddress: "127.0.0.1");
        Assert.True(matchPlateRes.Success);
        Assert.Equal(SessionStatuses.Completed, matchPlateRes.Data!.Status);
    }

    [Fact]
    public async Task Capacity_DisjointAndOverlappingOccupancy_CalculatesCorrectConservativeCapacity()
    {
        var (sessionService, _, context) = await CreateServicesAsync();
        var (lot, _, slots) = SeedLot(context, 222, "Bãi Capacity Calc", slotCount: 5);

        // Slot 0: Confirmed pair (car checks in to slot 0 when EMPTY, then IoT sensor detects OCCUPIED)
        var s1Res = await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 222, PlateNumber = "51A-PAIR-01", SlotId = slots[0].Id },
            actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1");
        Assert.True(s1Res.Success);
        slots[0].PhysicalStatus = SlotPhysicalStatuses.Occupied;

        // Slot 1: Unmatched physical occupancy (Occupied sensor without any backend session)
        slots[1].PhysicalStatus = SlotPhysicalStatuses.Occupied;

        // Session 2: Unassigned roaming walk-in session (Active, CurrentSlotId = null)
        var s2Res = await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 222, PlateNumber = "51A-ROAMING-02", SlotId = null },
            actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1");
        Assert.True(s2Res.Success);

        // Slot 4: Maintenance slot (must be excluded from usable capacity)
        slots[4].PhysicalStatus = SlotPhysicalStatuses.Maintenance;
        await context.SaveChangesAsync();

        var calc = await ParkingLotCapacityHelper.CalculateAsync(context, 222, lot);

        // Usable capacity: 4 (5 slots total - 1 maintenance = 4)
        Assert.Equal(4, calc.TotalCapacity);
        // Active sessions: 2
        Assert.Equal(2, calc.ActiveSessionsCount);
        // Physical occupied slots: 2 (slots[0] and slots[1])
        Assert.Equal(2, calc.PhysicalOccupiedSlotsCount);
        // Effective occupied: Matched (1) + Unassigned sessions (1) + Unmatched physical (1) = 3 (not undercounted as 2)
        Assert.Equal(3, calc.EffectiveOccupied);
        // Available capacity: 4 - 3 = 1
        Assert.Equal(1, calc.AvailableCapacity);
        // Unmatched physical occupied count: 1 (slots[1])
        Assert.Equal(1, calc.UnmatchedPhysicalOccupiedCount);
        // Unassigned sessions count: 1 (s2)
        Assert.Equal(1, calc.UnassignedSessionsCount);
    }

    [Fact]
    public async Task Capacity_ConservativeUndercountGuard_CalculatesExactlySeven()
    {
        // Test bắt buộc:
        // - 2 Session đã ghép IoT Slot
        // - 2 Session chưa ghép Slot
        // - 3 Slot OCCUPIED chưa ghép Session
        // - Expected conservative occupied count = 7, không phải 5.
        var (sessionService, _, context) = await CreateServicesAsync();
        var (lot, _, slots) = SeedLot(context, 223, "Bãi Capacity Test 7", slotCount: 10);

        // 2 matched pairs (Session assigned to Slot and Slot is Occupied)
        var s1 = await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 223, PlateNumber = "51A-MATCH-01", SlotId = slots[0].Id },
            actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1");
        slots[0].PhysicalStatus = SlotPhysicalStatuses.Occupied;

        var s2 = await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 223, PlateNumber = "51A-MATCH-02", SlotId = slots[1].Id },
            actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1");
        slots[1].PhysicalStatus = SlotPhysicalStatuses.Occupied;

        // 2 unassigned sessions (Active sessions without slot assigned)
        var s3 = await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 223, PlateNumber = "51A-UNASSIGNED-03", SlotId = null },
            actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1");
        var s4 = await sessionService.CheckInAsync(
            new CheckInRequestDto { LotId = 223, PlateNumber = "51A-UNASSIGNED-04", SlotId = null },
            actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1");

        // 3 slots OCCUPIED unmatched (no session linked to them)
        slots[2].PhysicalStatus = SlotPhysicalStatuses.Occupied;
        slots[3].PhysicalStatus = SlotPhysicalStatuses.Occupied;
        slots[4].PhysicalStatus = SlotPhysicalStatuses.Occupied;

        await context.SaveChangesAsync();

        var calc = await ParkingLotCapacityHelper.CalculateAsync(context, 223, lot);

        Assert.Equal(10, calc.TotalCapacity);
        Assert.Equal(4, calc.ActiveSessionsCount);
        Assert.Equal(5, calc.PhysicalOccupiedSlotsCount); // slots 0, 1, 2, 3, 4
        Assert.Equal(2, calc.MatchedPairsCount);
        Assert.Equal(2, calc.UnassignedSessionsCount);
        Assert.Equal(3, calc.UnmatchedPhysicalOccupiedCount);
        // Conservative occupied count = 2 matched + 2 unassigned + 3 unmatched = 7!
        Assert.Equal(7, calc.EffectiveOccupied);
        Assert.Equal(3, calc.AvailableCapacity); // 10 - 7 = 3
    }
}
