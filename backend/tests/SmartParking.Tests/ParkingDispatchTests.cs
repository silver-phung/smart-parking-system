using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.Application.DTOs.Dispatches;
using SmartParking.Application.DTOs.Sessions;
using SmartParking.Domain.Constants;
using SmartParking.Domain.Entities;
using SmartParking.Infrastructure.Services;
using Xunit;

namespace SmartParking.Tests;

public class ParkingDispatchTests
{
    private async Task<(ParkingDispatchService DispatchService, TestSmartParkingDbContext Context, ParkingLot Lot, ParkingFloor Floor, List<ParkingSlot> Slots, ParkingSession Session)> SetupTestEnvironmentAsync()
    {
        var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var dispatchService = new ParkingDispatchService(context, NullLogger<ParkingDispatchService>.Instance);

        var lot = new ParkingLot
        {
            Id = 1,
            Name = "Bãi A Trung Tâm",
            Address = "123 Lê Duẩn, Q1",
            TotalCapacity = 50,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.ParkingLots.Add(lot);

        var floor1 = new ParkingFloor
        {
            Id = 1,
            LotId = lot.Id,
            FloorNumber = 1,
            Name = "Tầng 1",
            TotalSlots = 10,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.ParkingFloors.Add(floor1);

        var slots = new List<ParkingSlot>
        {
            new()
            {
                Id = 1,
                FloorId = floor1.Id,
                SlotCode = "A1-01",
                PhysicalStatus = SlotPhysicalStatuses.Empty,
                IsActive = true,
                PositionX = 10.5m,
                PositionY = 0.0m,
                PositionZ = 5.2m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = 2,
                FloorId = floor1.Id,
                SlotCode = "A1-02",
                PhysicalStatus = SlotPhysicalStatuses.Empty,
                IsActive = true,
                PositionX = 12.5m,
                PositionY = 0.0m,
                PositionZ = 5.2m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = 3,
                FloorId = floor1.Id,
                SlotCode = "A1-03",
                PhysicalStatus = SlotPhysicalStatuses.Maintenance,
                IsActive = true,
                PositionX = 14.5m,
                PositionY = 0.0m,
                PositionZ = 5.2m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
        context.ParkingSlots.AddRange(slots);

        var userVehicle = new Vehicle
        {
            Id = 10,
            UserId = 100,
            PlateNumber = "51A-99999",
            Brand = "Honda",
            Model = "CR-V",
            Color = "White",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Vehicles.Add(userVehicle);

        var session = new ParkingSession
        {
            Id = 101,
            LotId = lot.Id,
            VehicleId = userVehicle.Id,
            PlateNumber = userVehicle.PlateNumber,
            CheckInTime = DateTime.UtcNow,
            Status = SessionStatuses.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.ParkingSessions.Add(session);

        await context.SaveChangesAsync();

        return (dispatchService, context, lot, floor1, slots, session);
    }

    #region CreateDispatch Tests

    [Fact]
    public async Task CreateDispatch_AutoMode_AssignsFirstAvailableSlot_AndSlotRemainsEmpty()
    {
        var (service, context, _, _, slots, session) = await SetupTestEnvironmentAsync();

        var request = new CreateDispatchRequestDto
        {
            SessionId = session.Id,
            AssignedSlotId = null, // Auto
            DispatchMode = DispatchModes.Auto,
            Notes = "Tự động phân ô đỗ"
        };

        var (success, message, dispatch) = await service.CreateDispatchAsync(request, 1, Roles.Admin, "127.0.0.1");

        Assert.True(success);
        Assert.NotNull(dispatch);
        Assert.Equal(DispatchStatuses.Assigned, dispatch.Status);
        Assert.Equal(slots[0].Id, dispatch.AssignedSlotId); // First empty slot is A1-01
        Assert.Equal("A1-01", dispatch.AssignedSlotCode);
        Assert.Equal(10.5m, dispatch.AssignedPositionX);
        Assert.Equal(5.2m, dispatch.AssignedPositionZ);

        // Quy tắc quan trọng: Check-in / Dispatch không tự chuyển ô sang OCCUPIED.
        // Ô đỗ vật lý vẫn giữ EMPTY cho đến khi có xác nhận đỗ thực tế!
        var slotInDb = await context.ParkingSlots.FindAsync(slots[0].Id);
        Assert.NotNull(slotInDb);
        Assert.Equal(SlotPhysicalStatuses.Empty, slotInDb.PhysicalStatus);
    }

    [Fact]
    public async Task CreateDispatch_ManualMode_AssignsRequestedSlot()
    {
        var (service, context, _, _, slots, session) = await SetupTestEnvironmentAsync();

        var request = new CreateDispatchRequestDto
        {
            SessionId = session.Id,
            AssignedSlotId = slots[1].Id, // A1-02
            DispatchMode = DispatchModes.Manual,
            Notes = "Gán thủ công ô A1-02"
        };

        var (success, message, dispatch) = await service.CreateDispatchAsync(request, 1, Roles.Admin, "127.0.0.1");

        Assert.True(success);
        Assert.NotNull(dispatch);
        Assert.Equal(slots[1].Id, dispatch.AssignedSlotId);
        Assert.Equal("A1-02", dispatch.AssignedSlotCode);
        Assert.Equal(DispatchModes.Manual, dispatch.DispatchMode);

        var slotInDb = await context.ParkingSlots.FindAsync(slots[1].Id);
        Assert.NotNull(slotInDb);
        Assert.Equal(SlotPhysicalStatuses.Empty, slotInDb.PhysicalStatus);
    }

    [Fact]
    public async Task CreateDispatch_SlotInMaintenance_Fails()
    {
        var (service, _, _, _, slots, session) = await SetupTestEnvironmentAsync();

        var request = new CreateDispatchRequestDto
        {
            SessionId = session.Id,
            AssignedSlotId = slots[2].Id, // A1-03 is MAINTENANCE
            DispatchMode = DispatchModes.Manual
        };

        var (success, message, dispatch) = await service.CreateDispatchAsync(request, 1, Roles.Admin, "127.0.0.1");

        Assert.False(success);
        Assert.Null(dispatch);
        Assert.Contains("không khả dụng", message);
    }

    [Fact]
    public async Task CreateDispatch_SessionNotActive_Fails()
    {
        var (service, context, _, _, slots, session) = await SetupTestEnvironmentAsync();
        session.Status = SessionStatuses.Completed;
        await context.SaveChangesAsync();

        var request = new CreateDispatchRequestDto
        {
            SessionId = session.Id,
            AssignedSlotId = slots[0].Id
        };

        var (success, message, dispatch) = await service.CreateDispatchAsync(request, 1, Roles.Admin, "127.0.0.1");

        Assert.False(success);
        Assert.Null(dispatch);
        Assert.Contains("ACTIVE", message);
    }

    [Fact]
    public async Task CreateDispatch_SlotBelongsToDifferentLot_Fails()
    {
        var (service, context, _, _, _, session) = await SetupTestEnvironmentAsync();

        var lot2 = new ParkingLot
        {
            Id = 2,
            Name = "Bãi B Phụ",
            TotalCapacity = 20,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var floorLot2 = new ParkingFloor
        {
            Id = 20,
            LotId = lot2.Id,
            FloorNumber = 1,
            Name = "Tầng 1 Lot B",
            TotalSlots = 10,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var slotLot2 = new ParkingSlot
        {
            Id = 201,
            FloorId = floorLot2.Id,
            SlotCode = "B1-01",
            PhysicalStatus = SlotPhysicalStatuses.Empty,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.ParkingLots.Add(lot2);
        context.ParkingFloors.Add(floorLot2);
        context.ParkingSlots.Add(slotLot2);
        await context.SaveChangesAsync();

        var request = new CreateDispatchRequestDto
        {
            SessionId = session.Id,
            AssignedSlotId = slotLot2.Id
        };

        var (success, message, dispatch) = await service.CreateDispatchAsync(request, 1, Roles.Admin, "127.0.0.1");

        Assert.False(success);
        Assert.Null(dispatch);
        Assert.Contains("không thuộc bãi đỗ xe", message);
    }

    [Fact]
    public async Task CreateDispatch_DuplicateActiveDispatchForSameSession_Fails()
    {
        var (service, _, _, _, slots, session) = await SetupTestEnvironmentAsync();

        var req1 = new CreateDispatchRequestDto
        {
            SessionId = session.Id,
            AssignedSlotId = slots[0].Id
        };
        var (success1, _, _) = await service.CreateDispatchAsync(req1, 1, Roles.Admin, "127.0.0.1");
        Assert.True(success1);

        var req2 = new CreateDispatchRequestDto
        {
            SessionId = session.Id,
            AssignedSlotId = slots[1].Id
        };
        var (success2, message2, dispatch2) = await service.CreateDispatchAsync(req2, 1, Roles.Admin, "127.0.0.1");

        Assert.False(success2);
        Assert.Null(dispatch2);
        Assert.Contains("lệnh điều phối chưa hoàn tất", message2);
    }

    [Fact]
    public async Task CreateDispatch_DoubleAssignmentToSameSlot_Fails()
    {
        var (service, context, lot, _, slots, session1) = await SetupTestEnvironmentAsync();

        // Tạo thêm session 2
        var session2 = new ParkingSession
        {
            Id = 102,
            LotId = lot.Id,
            PlateNumber = "29A-12345",
            CheckInTime = DateTime.UtcNow,
            Status = SessionStatuses.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.ParkingSessions.Add(session2);
        await context.SaveChangesAsync();

        // Gán slot 1 cho session 1
        var (s1, _, _) = await service.CreateDispatchAsync(new CreateDispatchRequestDto
        {
            SessionId = session1.Id,
            AssignedSlotId = slots[0].Id
        }, 1, Roles.Admin, "127.0.0.1");
        Assert.True(s1);

        // Gán cùng slot 1 cho session 2 -> Phải bị từ chối
        var (s2, message2, dispatch2) = await service.CreateDispatchAsync(new CreateDispatchRequestDto
        {
            SessionId = session2.Id,
            AssignedSlotId = slots[0].Id
        }, 1, Roles.Admin, "127.0.0.1");

        Assert.False(s2);
        Assert.Null(dispatch2);
        Assert.Contains("hiện đang được phân công", message2);
    }

    #endregion

    #region ReassignSlot Tests

    [Fact]
    public async Task ReassignSlot_ToAnotherEmptySlotInSameLot_Succeeds()
    {
        var (service, context, _, _, slots, session) = await SetupTestEnvironmentAsync();

        var (s1, _, d1) = await service.CreateDispatchAsync(new CreateDispatchRequestDto
        {
            SessionId = session.Id,
            AssignedSlotId = slots[0].Id
        }, 1, Roles.Admin, "127.0.0.1");
        Assert.True(s1);

        // Điều phối lại sang A1-02
        var reassignReq = new AssignSlotRequestDto
        {
            SlotId = slots[1].Id,
            Notes = "Đổi sang ô A1-02 do ô cũ bị vật cản"
        };
        var (s2, message2, d2) = await service.ReassignSlotAsync(d1!.Id, reassignReq, 1, Roles.Admin, "127.0.0.1");

        Assert.True(s2);
        Assert.NotNull(d2);
        Assert.Equal(slots[1].Id, d2.AssignedSlotId);
        Assert.Equal("A1-02", d2.AssignedSlotCode);

        // Cả 2 ô vẫn là EMPTY
        var slot1 = await context.ParkingSlots.FindAsync(slots[0].Id);
        var slot2 = await context.ParkingSlots.FindAsync(slots[1].Id);
        Assert.Equal(SlotPhysicalStatuses.Empty, slot1!.PhysicalStatus);
        Assert.Equal(SlotPhysicalStatuses.Empty, slot2!.PhysicalStatus);
    }

    #endregion

    #region ConfirmOccupied Tests

    [Fact]
    public async Task ConfirmOccupied_FlipsSlotToOccupied_AndSetsActualSlot()
    {
        var (service, context, _, _, slots, session) = await SetupTestEnvironmentAsync();

        var (s1, _, d1) = await service.CreateDispatchAsync(new CreateDispatchRequestDto
        {
            SessionId = session.Id,
            AssignedSlotId = slots[0].Id
        }, 1, Roles.Admin, "127.0.0.1");
        Assert.True(s1);

        var confirmReq = new ConfirmOccupiedRequestDto
        {
            Notes = "Nhân viên xác nhận xe đã vào đúng ô"
        };
        var (s2, message2, d2) = await service.ConfirmOccupiedAsync(d1!.Id, confirmReq, 1, Roles.Admin, "127.0.0.1");

        Assert.True(s2);
        Assert.NotNull(d2);
        Assert.Equal(DispatchStatuses.Occupied, d2.Status);
        Assert.Equal(slots[0].Id, d2.ActualSlotId);
        Assert.Equal("A1-01", d2.ActualSlotCode);
        Assert.NotNull(d2.ConfirmedAt);

        // Lúc này ô đỗ vật lý mới chính thức là OCCUPIED!
        var slotInDb = await context.ParkingSlots.FindAsync(slots[0].Id);
        Assert.Equal(SlotPhysicalStatuses.Occupied, slotInDb!.PhysicalStatus);
    }

    [Fact]
    public async Task ConfirmOccupied_WhenNotAssigned_Fails()
    {
        var (service, _, _, _, slots, session) = await SetupTestEnvironmentAsync();

        var (s1, _, d1) = await service.CreateDispatchAsync(new CreateDispatchRequestDto
        {
            SessionId = session.Id,
            AssignedSlotId = slots[0].Id
        }, 1, Roles.Admin, "127.0.0.1");

        await service.CancelDispatchAsync(d1!.Id, new CancelDispatchRequestDto { Reason = "Hủy" }, 1, Roles.Admin, "127.0.0.1");

        var (s2, message2, d2) = await service.ConfirmOccupiedAsync(d1.Id, new ConfirmOccupiedRequestDto(), 1, Roles.Admin, "127.0.0.1");
        Assert.False(s2);
        Assert.Null(d2);
        Assert.Contains("ASSIGNED", message2);
    }

    #endregion

    #region ReportWrongSlot Tests

    [Fact]
    public async Task ReportWrongSlot_ActualSlotBecomesOccupied_AssignedSlotRemainsEmpty()
    {
        var (service, context, _, _, slots, session) = await SetupTestEnvironmentAsync();

        // Gán ô A1-01
        var (s1, _, d1) = await service.CreateDispatchAsync(new CreateDispatchRequestDto
        {
            SessionId = session.Id,
            AssignedSlotId = slots[0].Id
        }, 1, Roles.Admin, "127.0.0.1");
        Assert.True(s1);

        // Xe lại thực tế đỗ vào ô A1-02
        var wrongReq = new ReportWrongSlotRequestDto
        {
            ActualSlotId = slots[1].Id,
            Notes = "Khách lái xe vào nhầm ô A1-02"
        };
        var (s2, message2, d2) = await service.ReportWrongSlotAsync(d1!.Id, wrongReq, 1, Roles.Admin, "127.0.0.1");

        Assert.True(s2);
        Assert.NotNull(d2);
        Assert.Equal(DispatchStatuses.WrongSlot, d2.Status);
        Assert.Equal(slots[0].Id, d2.AssignedSlotId);
        Assert.Equal("A1-01", d2.AssignedSlotCode);
        Assert.Equal(slots[1].Id, d2.ActualSlotId);
        Assert.Equal("A1-02", d2.ActualSlotCode);

        // Ô được chỉ định ban đầu (A1-01) vẫn là EMPTY (giải phóng cho xe khác)
        var assignedSlot = await context.ParkingSlots.FindAsync(slots[0].Id);
        Assert.Equal(SlotPhysicalStatuses.Empty, assignedSlot!.PhysicalStatus);

        // Ô thực tế (A1-02) chuyển sang OCCUPIED
        var actualSlot = await context.ParkingSlots.FindAsync(slots[1].Id);
        Assert.Equal(SlotPhysicalStatuses.Occupied, actualSlot!.PhysicalStatus);

        // Session currentSlotId được cập nhật về ô thực tế (A1-02)
        var sess = await context.ParkingSessions.FindAsync(session.Id);
        Assert.Equal(slots[1].Id, sess!.CurrentSlotId);
    }

    #endregion

    #region CancelDispatch Tests

    [Fact]
    public async Task CancelDispatch_ReleasesAssignment_SlotRemainsEmpty()
    {
        var (service, context, _, _, slots, session) = await SetupTestEnvironmentAsync();

        var (s1, _, d1) = await service.CreateDispatchAsync(new CreateDispatchRequestDto
        {
            SessionId = session.Id,
            AssignedSlotId = slots[0].Id
        }, 1, Roles.Admin, "127.0.0.1");
        Assert.True(s1);

        var (s2, message2) = await service.CancelDispatchAsync(d1!.Id, new CancelDispatchRequestDto
        {
            Reason = "Khách đổi ý không muốn gửi xe nữa"
        }, 1, Roles.Admin, "127.0.0.1");

        Assert.True(s2);

        var dispatchInDb = await context.ParkingDispatches.FindAsync(d1.Id);
        Assert.Equal(DispatchStatuses.Cancelled, dispatchInDb!.Status);
        Assert.Contains("[HỦY]", dispatchInDb.Notes);

        var slot = await context.ParkingSlots.FindAsync(slots[0].Id);
        Assert.Equal(SlotPhysicalStatuses.Empty, slot!.PhysicalStatus);

        var sess = await context.ParkingSessions.FindAsync(session.Id);
        Assert.Null(sess!.CurrentSlotId);
    }

    #endregion

    #region Query & 3D Coordinates & RBAC Tests

    [Fact]
    public async Task GetDispatchById_Includes3DCoordinates()
    {
        var (service, _, _, _, slots, session) = await SetupTestEnvironmentAsync();

        var (s1, _, d1) = await service.CreateDispatchAsync(new CreateDispatchRequestDto
        {
            SessionId = session.Id,
            AssignedSlotId = slots[0].Id
        }, 1, Roles.Admin, "127.0.0.1");

        var retrieved = await service.GetDispatchByIdAsync(d1!.Id, currentUserId: null, isAdmin: true);

        Assert.NotNull(retrieved);
        Assert.Equal(10.5m, retrieved.AssignedPositionX);
        Assert.Equal(0.0m, retrieved.AssignedPositionY);
        Assert.Equal(5.2m, retrieved.AssignedPositionZ);
        Assert.Equal("Tầng 1", retrieved.AssignedFloorName);
        Assert.Equal(1, retrieved.AssignedFloorNumber);
    }

    [Fact]
    public async Task GetDispatchById_UserCanOnlyViewOwnVehicleDispatch()
    {
        var (service, _, _, _, slots, session) = await SetupTestEnvironmentAsync();

        var (s1, _, d1) = await service.CreateDispatchAsync(new CreateDispatchRequestDto
        {
            SessionId = session.Id,
            AssignedSlotId = slots[0].Id
        }, 1, Roles.Admin, "127.0.0.1");

        // session.Vehicle.UserId = 100
        // Owner queries -> Success
        var ownerResult = await service.GetDispatchByIdAsync(d1!.Id, currentUserId: 100, isAdmin: false);
        Assert.NotNull(ownerResult);

        // Different user queries -> Null
        var otherUserResult = await service.GetDispatchByIdAsync(d1.Id, currentUserId: 999, isAdmin: false);
        Assert.Null(otherUserResult);
    }

    [Fact]
    public async Task GetActiveDispatchBySessionId_ReturnsActiveOnly()
    {
        var (service, _, _, _, slots, session) = await SetupTestEnvironmentAsync();

        var (s1, _, d1) = await service.CreateDispatchAsync(new CreateDispatchRequestDto
        {
            SessionId = session.Id,
            AssignedSlotId = slots[0].Id
        }, 1, Roles.Admin, "127.0.0.1");

        var active = await service.GetActiveDispatchBySessionIdAsync(session.Id, currentUserId: null, isAdmin: true);
        Assert.NotNull(active);
        Assert.Equal(d1!.Id, active.Id);

        // When cancelled, it should no longer be active
        await service.CancelDispatchAsync(d1.Id, new CancelDispatchRequestDto { Reason = "test" }, 1, Roles.Admin, "127.0.0.1");
        var activeAfterCancel = await service.GetActiveDispatchBySessionIdAsync(session.Id, currentUserId: null, isAdmin: true);
        Assert.Null(activeAfterCancel);
    }

    #endregion

    #region Synchronization & Conflict Edge Cases

    [Fact]
    public async Task ConfirmOccupied_WhenSlotAlreadyOccupied_FailsWithConflict()
    {
        var (service, context, _, _, slots, session) = await SetupTestEnvironmentAsync();

        var (s1, _, d1) = await service.CreateDispatchAsync(new CreateDispatchRequestDto
        {
            SessionId = session.Id,
            AssignedSlotId = slots[0].Id
        }, 1, Roles.Admin, "127.0.0.1");
        Assert.True(s1);

        // Giả sử ô A1-01 đột nhiên bị một xe khác chiếm (OCCUPIED)
        var slotInDb = await context.ParkingSlots.FindAsync(slots[0].Id);
        slotInDb!.PhysicalStatus = SlotPhysicalStatuses.Occupied;
        await context.SaveChangesAsync();

        var (s2, message2, d2) = await service.ConfirmOccupiedAsync(d1!.Id, new ConfirmOccupiedRequestDto(), 1, Roles.Admin, "127.0.0.1");

        Assert.False(s2);
        Assert.Null(d2);
        Assert.Contains("Xung đột chiếm ô", message2);
    }

    [Fact]
    public async Task ReportWrongSlot_WhenActualSlotAlreadyOccupied_FailsWithConflict()
    {
        var (service, context, _, _, slots, session) = await SetupTestEnvironmentAsync();

        var (s1, _, d1) = await service.CreateDispatchAsync(new CreateDispatchRequestDto
        {
            SessionId = session.Id,
            AssignedSlotId = slots[0].Id
        }, 1, Roles.Admin, "127.0.0.1");
        Assert.True(s1);

        // Ô thực tế A1-02 đã bị chiếm trước đó
        var slot2 = await context.ParkingSlots.FindAsync(slots[1].Id);
        slot2!.PhysicalStatus = SlotPhysicalStatuses.Occupied;
        await context.SaveChangesAsync();

        var (s2, message2, d2) = await service.ReportWrongSlotAsync(d1!.Id, new ReportWrongSlotRequestDto
        {
            ActualSlotId = slots[1].Id
        }, 1, Roles.Admin, "127.0.0.1");

        Assert.False(s2);
        Assert.Null(d2);
        Assert.Contains("Xung đột chiếm ô", message2);
    }

    [Fact]
    public async Task ReportWrongSlot_WhenActualSlotAssignedToAnotherDispatch_FailsWithConflict()
    {
        var (service, context, lot, _, slots, session1) = await SetupTestEnvironmentAsync();

        // Session 2 gán ô A1-02
        var session2 = new ParkingSession
        {
            Id = 102,
            LotId = lot.Id,
            PlateNumber = "29A-88888",
            CheckInTime = DateTime.UtcNow,
            Status = SessionStatuses.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.ParkingSessions.Add(session2);
        await context.SaveChangesAsync();

        var (s2, _, d2) = await service.CreateDispatchAsync(new CreateDispatchRequestDto
        {
            SessionId = session2.Id,
            AssignedSlotId = slots[1].Id // A1-02
        }, 1, Roles.Admin, "127.0.0.1");
        Assert.True(s2);

        // Session 1 gán ô A1-01
        var (s1, _, d1) = await service.CreateDispatchAsync(new CreateDispatchRequestDto
        {
            SessionId = session1.Id,
            AssignedSlotId = slots[0].Id // A1-01
        }, 1, Roles.Admin, "127.0.0.1");
        Assert.True(s1);

        // Session 1 cố báo nhầm vào ô A1-02 vốn đang được gán cho Session 2
        var (sReport, msgReport, _) = await service.ReportWrongSlotAsync(d1!.Id, new ReportWrongSlotRequestDto
        {
            ActualSlotId = slots[1].Id
        }, 1, Roles.Admin, "127.0.0.1");

        Assert.False(sReport);
        Assert.Contains("Xung đột gán ô", msgReport);
    }

    [Fact]
    public async Task CancelDispatch_WhenSlotIsOccupied_DoesNotResetPhysicalStatusToEmpty()
    {
        var (service, context, _, _, slots, session) = await SetupTestEnvironmentAsync();

        var (s1, _, d1) = await service.CreateDispatchAsync(new CreateDispatchRequestDto
        {
            SessionId = session.Id,
            AssignedSlotId = slots[0].Id
        }, 1, Roles.Admin, "127.0.0.1");
        Assert.True(s1);

        // Giả sử có sự cố ô này đã bị đánh dấu OCCUPIED
        var slot1 = await context.ParkingSlots.FindAsync(slots[0].Id);
        slot1!.PhysicalStatus = SlotPhysicalStatuses.Occupied;
        await context.SaveChangesAsync();

        var (sCancel, _) = await service.CancelDispatchAsync(d1!.Id, new CancelDispatchRequestDto { Reason = "test" }, 1, Roles.Admin, "127.0.0.1");
        Assert.True(sCancel);

        // Ô đỗ vẫn phải giữ nguyên là OCCUPIED, không được vô tình giải phóng thành EMPTY
        var slotAfterCancel = await context.ParkingSlots.FindAsync(slots[0].Id);
        Assert.Equal(SlotPhysicalStatuses.Occupied, slotAfterCancel!.PhysicalStatus);
    }

    [Fact]
    public async Task CheckIn_WhenSlotAlreadyAssignedToActiveDispatch_Fails()
    {
        var (dispatchService, context, lot, _, slots, session1) = await SetupTestEnvironmentAsync();
        var sessionService = new ParkingSessionService(context, NullLogger<ParkingSessionService>.Instance);

        // Gán ô A1-01 cho session 1
        var (s1, _, _) = await dispatchService.CreateDispatchAsync(new CreateDispatchRequestDto
        {
            SessionId = session1.Id,
            AssignedSlotId = slots[0].Id
        }, 1, Roles.Admin, "127.0.0.1");
        Assert.True(s1);

        // Xe khác check-in và trực tiếp chỉ định ô A1-01 -> Phải bị từ chối
        var checkInReq = new CheckInRequestDto
        {
            LotId = lot.Id,
            PlateNumber = "30A-99999",
            SlotId = slots[0].Id
        };
        var (sCheckIn, msgCheckIn, _) = await sessionService.CheckInAsync(checkInReq, 1, Roles.Admin, "127.0.0.1");

        Assert.False(sCheckIn);
        Assert.Contains("hiện đang được phân công", msgCheckIn);
    }

    [Fact]
    public async Task CheckOut_WhenSessionHasPendingOrAssignedDispatch_CancelsDispatchAndFreesSlot()
    {
        var (dispatchService, context, _, _, slots, session) = await SetupTestEnvironmentAsync();
        var sessionService = new ParkingSessionService(context, NullLogger<ParkingSessionService>.Instance);

        var (s1, _, d1) = await dispatchService.CreateDispatchAsync(new CreateDispatchRequestDto
        {
            SessionId = session.Id,
            AssignedSlotId = slots[0].Id
        }, 1, Roles.Admin, "127.0.0.1");
        Assert.True(s1);

        // Check-out khi dispatch vẫn đang ASSIGNED (chưa đỗ thực tế)
        var (sCheckOut, _, sessionDto) = await sessionService.CheckOutAsync(session.Id, new CheckOutRequestDto(), 1, Roles.Admin, "127.0.0.1");
        Assert.True(sCheckOut);

        // Dispatch phải được tự động chuyển sang CANCELLED, không để tồn đọng
        var dispatchInDb = await context.ParkingDispatches.FindAsync(d1!.Id);
        Assert.Equal(DispatchStatuses.Cancelled, dispatchInDb!.Status);
        Assert.Contains("[CHECK-OUT]", dispatchInDb.Notes);

        // Ô đỗ vẫn an toàn ở trạng thái EMPTY
        var slotInDb = await context.ParkingSlots.FindAsync(slots[0].Id);
        Assert.Equal(SlotPhysicalStatuses.Empty, slotInDb!.PhysicalStatus);
    }

    [Fact]
    public async Task FullLifecycle_CheckIn_Dispatch_ConfirmOccupied_CheckOut_SlotReturnsToEmpty()
    {
        var (dispatchService, context, _, _, slots, session) = await SetupTestEnvironmentAsync();
        var sessionService = new ParkingSessionService(context, NullLogger<ParkingSessionService>.Instance);

        // 1. Dispatch gán A1-01
        var (s1, _, d1) = await dispatchService.CreateDispatchAsync(new CreateDispatchRequestDto
        {
            SessionId = session.Id,
            AssignedSlotId = slots[0].Id
        }, 1, Roles.Admin, "127.0.0.1");
        Assert.True(s1);

        // 2. Xác nhận xe đỗ đúng ô -> Ô trở thành OCCUPIED
        var (s2, _, _) = await dispatchService.ConfirmOccupiedAsync(d1!.Id, new ConfirmOccupiedRequestDto(), 1, Roles.Admin, "127.0.0.1");
        Assert.True(s2);
        var slotOccupied = await context.ParkingSlots.FindAsync(slots[0].Id);
        Assert.Equal(SlotPhysicalStatuses.Occupied, slotOccupied!.PhysicalStatus);

        // 3. Check-out -> Ô đỗ trở về EMPTY
        var (s3, _, _) = await sessionService.CheckOutAsync(session.Id, new CheckOutRequestDto(), 1, Roles.Admin, "127.0.0.1");
        Assert.True(s3);
        var slotFreed = await context.ParkingSlots.FindAsync(slots[0].Id);
        Assert.Equal(SlotPhysicalStatuses.Empty, slotFreed!.PhysicalStatus);
    }

    [Fact]
    public async Task FullLifecycle_CheckIn_Dispatch_WrongSlot_CheckOut_ReleasesActualSlot()
    {
        var (dispatchService, context, _, _, slots, session) = await SetupTestEnvironmentAsync();
        var sessionService = new ParkingSessionService(context, NullLogger<ParkingSessionService>.Instance);

        // 1. Dispatch gán A1-01
        var (s1, _, d1) = await dispatchService.CreateDispatchAsync(new CreateDispatchRequestDto
        {
            SessionId = session.Id,
            AssignedSlotId = slots[0].Id
        }, 1, Roles.Admin, "127.0.0.1");
        Assert.True(s1);

        // 2. Xe đỗ sai sang ô A1-02 -> A1-01 giữ EMPTY, A1-02 thành OCCUPIED
        var (s2, _, _) = await dispatchService.ReportWrongSlotAsync(d1!.Id, new ReportWrongSlotRequestDto
        {
            ActualSlotId = slots[1].Id
        }, 1, Roles.Admin, "127.0.0.1");
        Assert.True(s2);

        var slotAssigned = await context.ParkingSlots.FindAsync(slots[0].Id);
        var slotActual = await context.ParkingSlots.FindAsync(slots[1].Id);
        Assert.Equal(SlotPhysicalStatuses.Empty, slotAssigned!.PhysicalStatus);
        Assert.Equal(SlotPhysicalStatuses.Occupied, slotActual!.PhysicalStatus);

        // 3. Check-out -> Phải giải phóng ô thực tế A1-02 về EMPTY
        var (s3, _, _) = await sessionService.CheckOutAsync(session.Id, new CheckOutRequestDto(), 1, Roles.Admin, "127.0.0.1");
        Assert.True(s3);

        var slotActualFreed = await context.ParkingSlots.FindAsync(slots[1].Id);
        Assert.Equal(SlotPhysicalStatuses.Empty, slotActualFreed!.PhysicalStatus);
    }

    #endregion
}
