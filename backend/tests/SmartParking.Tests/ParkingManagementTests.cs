using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.Api.Controllers;
using SmartParking.Application.DTOs.Parking;
using SmartParking.Domain.Constants;
using SmartParking.Domain.Entities;
using SmartParking.Infrastructure.Services;
using Xunit;

namespace SmartParking.Tests;

public class ParkingManagementTests
{
    private async Task<(ParkingManagementService Service, TestSmartParkingDbContext Context)> CreateServiceAsync()
    {
        var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var logger = NullLogger<ParkingManagementService>.Instance;
        var service = new ParkingManagementService(context, logger);
        return (service, context);
    }

    #region Parking Lots Tests

    [Fact]
    public async Task CreateLot_ValidRequest_CreatesLotSuccessfully()
    {
        var (service, context) = await CreateServiceAsync();

        var request = new CreateParkingLotRequestDto
        {
            Name = "Bãi đỗ xe Trung tâm",
            Address = "123 Nguyễn Huệ, Q.1",
            TotalCapacity = 200,
            IsActive = true
        };

        var (success, message, lot) = await service.CreateLotAsync(request, 1);

        Assert.True(success);
        Assert.NotNull(lot);
        Assert.Equal("Bãi đỗ xe Trung tâm", lot.Name);
        Assert.Equal(200, lot.TotalCapacity);
        Assert.True(lot.IsActive);

        // Verify audit log
        var audit = context.AuditLogs.FirstOrDefault(a => a.Action == "CREATE_PARKING_LOT");
        Assert.NotNull(audit);
        Assert.Equal("parking_lots", audit.TargetEntity);
    }

    [Fact]
    public async Task CreateLot_DuplicateName_ReturnsFailure()
    {
        var (service, context) = await CreateServiceAsync();
        context.ParkingLots.Add(new ParkingLot { Name = "Bãi A", TotalCapacity = 50, IsActive = true });
        await context.SaveChangesAsync();

        var request = new CreateParkingLotRequestDto { Name = "  bãi a  ", TotalCapacity = 100 };
        var (success, message, lot) = await service.CreateLotAsync(request, 1);

        Assert.False(success);
        Assert.Null(lot);
        Assert.Contains("đã tồn tại", message);
    }

    [Fact]
    public async Task GetLots_FiltersAndPagination_WorkCorrectly()
    {
        var (service, context) = await CreateServiceAsync();
        context.ParkingLots.AddRange(
            new ParkingLot { Name = "Bãi Alpha", IsActive = true },
            new ParkingLot { Name = "Bãi Beta", IsActive = false },
            new ParkingLot { Name = "Bãi Gamma", IsActive = true }
        );
        await context.SaveChangesAsync();

        // Guest view (only active)
        var guestResult = await service.GetLotsAsync(new ParkingLotFilterDto { PageNumber = 1, PageSize = 10 }, isAdminOrStaff: false);
        Assert.Equal(2, guestResult.TotalCount);
        Assert.DoesNotContain(guestResult.Items, l => l.Name == "Bãi Beta");

        // Admin view (all lots)
        var adminResult = await service.GetLotsAsync(new ParkingLotFilterDto { PageNumber = 1, PageSize = 10 }, isAdminOrStaff: true);
        Assert.Equal(3, adminResult.TotalCount);

        // Search
        var searchResult = await service.GetLotsAsync(new ParkingLotFilterDto { Search = "alpha" }, isAdminOrStaff: true);
        Assert.Single(searchResult.Items);
        Assert.Equal("Bãi Alpha", searchResult.Items[0].Name);
    }

    [Fact]
    public async Task UpdateLotStatus_TogglesActiveStateAndAudits()
    {
        var (service, context) = await CreateServiceAsync();
        var lot = new ParkingLot { Name = "Bãi Active", IsActive = true };
        context.ParkingLots.Add(lot);
        await context.SaveChangesAsync();

        var (success, message, updated) = await service.UpdateLotStatusAsync(lot.Id, new UpdateLotStatusRequestDto { IsActive = false, Reason = "Bảo trì nâng cấp" }, 1);

        Assert.True(success);
        Assert.NotNull(updated);
        Assert.False(updated.IsActive);

        var audit = context.AuditLogs.FirstOrDefault(a => a.Action == "UPDATE_LOT_STATUS");
        Assert.NotNull(audit);
        Assert.Contains("Bảo trì nâng cấp", audit.NewValues);
    }

    [Fact]
    public async Task DeleteLot_WithFloorsOrSessions_RejectsHardDelete()
    {
        var (service, context) = await CreateServiceAsync();
        var lot = new ParkingLot { Name = "Bãi Có Tầng", IsActive = true };
        context.ParkingLots.Add(lot);
        await context.SaveChangesAsync();

        context.ParkingFloors.Add(new ParkingFloor { LotId = lot.Id, FloorNumber = 1, Name = "Tầng 1" });
        await context.SaveChangesAsync();

        var (success, message) = await service.DeleteLotAsync(lot.Id, 1);

        Assert.False(success);
        Assert.Contains("Không thể xóa bãi đỗ xe đang chứa các tầng", message);
    }

    #endregion

    #region Parking Floors Tests

    [Fact]
    public async Task CreateFloor_Valid_CreatesFloorSuccessfully()
    {
        var (service, context) = await CreateServiceAsync();
        var lot = new ParkingLot { Name = "Bãi F", IsActive = true };
        context.ParkingLots.Add(lot);
        await context.SaveChangesAsync();

        var request = new CreateParkingFloorRequestDto
        {
            FloorNumber = 1,
            Name = "Tầng Hầm B1",
            TotalSlots = 50,
            IsActive = true
        };

        var (success, message, floor) = await service.CreateFloorAsync(lot.Id, request, 1);

        Assert.True(success);
        Assert.NotNull(floor);
        Assert.Equal(1, floor.FloorNumber);
        Assert.Equal("Tầng Hầm B1", floor.Name);
    }

    [Fact]
    public async Task CreateFloor_DuplicateFloorNumberInSameLot_Rejects()
    {
        var (service, context) = await CreateServiceAsync();
        var lot = new ParkingLot { Name = "Bãi F2", IsActive = true };
        context.ParkingLots.Add(lot);
        await context.SaveChangesAsync();

        context.ParkingFloors.Add(new ParkingFloor { LotId = lot.Id, FloorNumber = 1, Name = "Tầng 1" });
        await context.SaveChangesAsync();

        var (success, message, floor) = await service.CreateFloorAsync(lot.Id, new CreateParkingFloorRequestDto
        {
            FloorNumber = 1,
            Name = "Tầng 1 mới"
        }, 1);

        Assert.False(success);
        Assert.Contains("đã tồn tại", message);
    }

    [Fact]
    public async Task DeleteFloor_WithSlots_RejectsHardDelete()
    {
        var (service, context) = await CreateServiceAsync();
        var lot = new ParkingLot { Name = "Bãi F3", IsActive = true };
        context.ParkingLots.Add(lot);
        await context.SaveChangesAsync();

        var floor = new ParkingFloor { LotId = lot.Id, FloorNumber = 1, Name = "Tầng 1" };
        context.ParkingFloors.Add(floor);
        await context.SaveChangesAsync();

        context.ParkingSlots.Add(new ParkingSlot { FloorId = floor.Id, SlotCode = "A01", PhysicalStatus = SlotPhysicalStatuses.Empty });
        await context.SaveChangesAsync();

        var (success, message) = await service.DeleteFloorAsync(floor.Id, 1);

        Assert.False(success);
        Assert.Contains("Không thể xóa tầng đang chứa các ô đỗ", message);
    }

    #endregion

    #region Parking Slots Tests

    [Fact]
    public async Task CreateSlot_Valid_NormalizesSlotCodeAndCreates()
    {
        var (service, context) = await CreateServiceAsync();
        var lot = new ParkingLot { Name = "Bãi S", IsActive = true };
        context.ParkingLots.Add(lot);
        await context.SaveChangesAsync();

        var floor = new ParkingFloor { LotId = lot.Id, FloorNumber = 1, Name = "Tầng 1" };
        context.ParkingFloors.Add(floor);
        await context.SaveChangesAsync();

        var request = new CreateParkingSlotRequestDto
        {
            SlotCode = " a-01 ",
            PhysicalStatus = SlotPhysicalStatuses.Empty,
            PositionX = 10.5m,
            PositionY = 20.0m
        };

        var (success, message, slot) = await service.CreateSlotAsync(floor.Id, request, 1);

        Assert.True(success);
        Assert.NotNull(slot);
        Assert.Equal("A-01", slot.SlotCode);
        Assert.Equal(SlotPhysicalStatuses.Empty, slot.PhysicalStatus);
        Assert.True(slot.IsAvailable);
    }

    [Fact]
    public async Task CreateSlot_DuplicateCodeInSameFloor_Rejects()
    {
        var (service, context) = await CreateServiceAsync();
        var lot = new ParkingLot { Name = "Bãi S2", IsActive = true };
        context.ParkingLots.Add(lot);
        await context.SaveChangesAsync();

        var floor = new ParkingFloor { LotId = lot.Id, FloorNumber = 1, Name = "Tầng 1" };
        context.ParkingFloors.Add(floor);
        await context.SaveChangesAsync();

        context.ParkingSlots.Add(new ParkingSlot { FloorId = floor.Id, SlotCode = "A-01", PhysicalStatus = SlotPhysicalStatuses.Empty });
        await context.SaveChangesAsync();

        var (success, message, slot) = await service.CreateSlotAsync(floor.Id, new CreateParkingSlotRequestDto
        {
            SlotCode = "A-01"
        }, 1);

        Assert.False(success);
        Assert.Contains("đã tồn tại trong tầng này", message);
    }

    [Fact]
    public async Task GetSlotById_PrivacyCheck_HidesVehicleForPublic()
    {
        var (service, context) = await CreateServiceAsync();
        var lot = new ParkingLot { Name = "Bãi P", IsActive = true };
        context.ParkingLots.Add(lot);
        await context.SaveChangesAsync();

        var floor = new ParkingFloor { LotId = lot.Id, FloorNumber = 1, Name = "Tầng 1" };
        context.ParkingFloors.Add(floor);
        await context.SaveChangesAsync();

        var slot = new ParkingSlot { FloorId = floor.Id, SlotCode = "A-01", PhysicalStatus = SlotPhysicalStatuses.Occupied };
        context.ParkingSlots.Add(slot);
        await context.SaveChangesAsync();

        var session = new ParkingSession
        {
            LotId = lot.Id,
            PlateNumber = "51A-12345",
            CurrentSlotId = slot.Id,
            Status = SessionStatuses.Active,
            CheckInTime = DateTime.UtcNow
        };
        context.ParkingSessions.Add(session);
        await context.SaveChangesAsync();

        // 1. Guest view: Should NOT reveal plate number
        var guestDetail = await service.GetSlotByIdAsync(slot.Id, includeInternalDetails: false);
        Assert.NotNull(guestDetail);
        Assert.Null(guestDetail.CurrentVehiclePlate);
        Assert.Null(guestDetail.CurrentSessionId);

        // 2. Internal Admin view: Should reveal plate number
        var internalDetail = await service.GetSlotByIdAsync(slot.Id, includeInternalDetails: true);
        Assert.NotNull(internalDetail);
        Assert.Equal("51A-12345", internalDetail.CurrentVehiclePlate);
        Assert.Equal(session.Id, internalDetail.CurrentSessionId);
    }

    [Fact]
    public async Task GetSlotById_ManagerAndStaff_ReceiveOnlyPublicDataWithoutVehicleDetails()
    {
        var (service, context) = await CreateServiceAsync();
        var lot = new ParkingLot { Name = "Bãi Private", IsActive = true };
        context.ParkingLots.Add(lot);
        await context.SaveChangesAsync();

        var floor = new ParkingFloor { LotId = lot.Id, FloorNumber = 1, Name = "Tầng 1" };
        context.ParkingFloors.Add(floor);
        await context.SaveChangesAsync();

        var slot = new ParkingSlot { FloorId = floor.Id, SlotCode = "P-01", PhysicalStatus = SlotPhysicalStatuses.Occupied };
        context.ParkingSlots.Add(slot);
        await context.SaveChangesAsync();

        var session = new ParkingSession
        {
            LotId = lot.Id,
            PlateNumber = "51F-88888",
            CurrentSlotId = slot.Id,
            Status = SessionStatuses.Active,
            CheckInTime = DateTime.UtcNow
        };
        context.ParkingSessions.Add(session);
        await context.SaveChangesAsync();

        // Non-admin (Staff, Manager, User, Guest) gets includeInternalDetails = false:
        var nonAdminResult = await service.GetSlotByIdAsync(slot.Id, includeInternalDetails: false);
        Assert.NotNull(nonAdminResult);
        Assert.Null(nonAdminResult.CurrentVehiclePlate);
        Assert.Null(nonAdminResult.CurrentSessionId);

        // Only Admin gets includeInternalDetails = true:
        var adminResult = await service.GetSlotByIdAsync(slot.Id, includeInternalDetails: true);
        Assert.NotNull(adminResult);
        Assert.Equal("51F-88888", adminResult.CurrentVehiclePlate);
        Assert.Equal(session.Id, adminResult.CurrentSessionId);
    }

    [Fact]
    public async Task UpdateSlotPhysicalStatus_OccupiedSlotWithActiveSession_RejectsManualChangeToEmpty()
    {
        var (service, context) = await CreateServiceAsync();
        var lot = new ParkingLot { Name = "Bãi O", IsActive = true };
        context.ParkingLots.Add(lot);
        await context.SaveChangesAsync();

        var floor = new ParkingFloor { LotId = lot.Id, FloorNumber = 1, Name = "Tầng 1" };
        context.ParkingFloors.Add(floor);
        await context.SaveChangesAsync();

        var slot = new ParkingSlot { FloorId = floor.Id, SlotCode = "A-01", PhysicalStatus = SlotPhysicalStatuses.Occupied };
        context.ParkingSlots.Add(slot);
        await context.SaveChangesAsync();

        var session = new ParkingSession
        {
            LotId = lot.Id,
            PlateNumber = "51A-99999",
            CurrentSlotId = slot.Id,
            Status = SessionStatuses.Active,
            CheckInTime = DateTime.UtcNow
        };
        context.ParkingSessions.Add(session);
        await context.SaveChangesAsync();

        // Attempting to manually change OCCUPIED to EMPTY while session is ACTIVE
        var (success, message, updatedSlot) = await service.UpdateSlotPhysicalStatusAsync(
            slot.Id,
            new UpdateSlotPhysicalStatusRequestDto { PhysicalStatus = SlotPhysicalStatuses.Empty },
            actorId: 2,
            actorRole: Roles.Manager,
            ipAddress: "127.0.0.1");

        // MUST be rejected by business rule
        Assert.False(success);
        Assert.Contains("Không thể chuyển trạng thái ô đỗ đang có xe (OCCUPIED)", message);
    }

    [Fact]
    public async Task UpdateSlotPhysicalStatus_EmptyToMaintenance_SucceedsAndAudits()
    {
        var (service, context) = await CreateServiceAsync();
        var lot = new ParkingLot { Name = "Bãi M", IsActive = true };
        context.ParkingLots.Add(lot);
        await context.SaveChangesAsync();

        var floor = new ParkingFloor { LotId = lot.Id, FloorNumber = 1, Name = "Tầng 1" };
        context.ParkingFloors.Add(floor);
        await context.SaveChangesAsync();

        var slot = new ParkingSlot { FloorId = floor.Id, SlotCode = "A-01", PhysicalStatus = SlotPhysicalStatuses.Empty };
        context.ParkingSlots.Add(slot);
        await context.SaveChangesAsync();

        var (success, message, updatedSlot) = await service.UpdateSlotPhysicalStatusAsync(
            slot.Id,
            new UpdateSlotPhysicalStatusRequestDto { PhysicalStatus = SlotPhysicalStatuses.Maintenance, Reason = "Hỏng cảm biến" },
            actorId: 3,
            actorRole: Roles.Staff,
            ipAddress: "192.168.1.10");

        Assert.True(success);
        Assert.NotNull(updatedSlot);
        Assert.Equal(SlotPhysicalStatuses.Maintenance, updatedSlot.PhysicalStatus);
        Assert.False(updatedSlot.IsAvailable);

        var audit = context.AuditLogs.FirstOrDefault(a => a.Action == "UPDATE_SLOT_PHYSICAL_STATUS");
        Assert.NotNull(audit);
        Assert.Equal(3, audit.ActorId);
        Assert.Contains("Hỏng cảm biến", audit.NewValues);
        Assert.Contains(Roles.Staff, audit.NewValues);
    }

    #endregion

    #region Controller Authorization & Route Attributes Verification

    [Fact]
    public void ParkingLotsController_HasCorrectAuthorizationPolicies()
    {
        var createMethod = typeof(ParkingLotsController).GetMethod(nameof(ParkingLotsController.CreateLot));
        var createAuth = createMethod?.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(createAuth);
        Assert.Equal(Roles.Admin, createAuth.Roles);

        var updateMethod = typeof(ParkingLotsController).GetMethod(nameof(ParkingLotsController.UpdateLot));
        var updateAuth = updateMethod?.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(updateAuth);
        Assert.Equal(Roles.Admin, updateAuth.Roles);

        var patchStatusMethod = typeof(ParkingLotsController).GetMethod(nameof(ParkingLotsController.UpdateLotStatus));
        var patchStatusAuth = patchStatusMethod?.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(patchStatusAuth);
        Assert.Equal($"{Roles.Admin},{Roles.Manager}", patchStatusAuth.Roles);

        var deleteMethod = typeof(ParkingLotsController).GetMethod(nameof(ParkingLotsController.DeleteLot));
        var deleteAuth = deleteMethod?.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(deleteAuth);
        Assert.Equal(Roles.Admin, deleteAuth.Roles);

        var getMethod = typeof(ParkingLotsController).GetMethod(nameof(ParkingLotsController.GetLots));
        var getAllowAnon = getMethod?.GetCustomAttribute<AllowAnonymousAttribute>();
        Assert.NotNull(getAllowAnon);

        var getByIdMethod = typeof(ParkingLotsController).GetMethod(nameof(ParkingLotsController.GetLotById));
        var getByIdAllowAnon = getByIdMethod?.GetCustomAttribute<AllowAnonymousAttribute>();
        Assert.NotNull(getByIdAllowAnon);
    }

    [Fact]
    public void ParkingFloorsController_HasCorrectRoleRestrictions()
    {
        var createMethod = typeof(ParkingFloorsController).GetMethod(nameof(ParkingFloorsController.CreateFloor));
        var createAuth = createMethod?.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(createAuth);
        Assert.Equal(Roles.Admin, createAuth.Roles);

        var updateMethod = typeof(ParkingFloorsController).GetMethod(nameof(ParkingFloorsController.UpdateFloor));
        var updateAuth = updateMethod?.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(updateAuth);
        Assert.Equal(Roles.Admin, updateAuth.Roles);

        var deleteMethod = typeof(ParkingFloorsController).GetMethod(nameof(ParkingFloorsController.DeleteFloor));
        var deleteAuth = deleteMethod?.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(deleteAuth);
        Assert.Equal(Roles.Admin, deleteAuth.Roles);

        var getByLotMethod = typeof(ParkingFloorsController).GetMethod(nameof(ParkingFloorsController.GetFloorsByLotId));
        var getByLotAllowAnon = getByLotMethod?.GetCustomAttribute<AllowAnonymousAttribute>();
        Assert.NotNull(getByLotAllowAnon);

        var getByIdMethod = typeof(ParkingFloorsController).GetMethod(nameof(ParkingFloorsController.GetFloorById));
        var getByIdAllowAnon = getByIdMethod?.GetCustomAttribute<AllowAnonymousAttribute>();
        Assert.NotNull(getByIdAllowAnon);
    }

    [Fact]
    public void ParkingSlotsController_HasCorrectRoleRestrictions()
    {
        var createMethod = typeof(ParkingSlotsController).GetMethod(nameof(ParkingSlotsController.CreateSlot));
        var createAuth = createMethod?.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(createAuth);
        Assert.Equal(Roles.Admin, createAuth.Roles);

        var updateMethod = typeof(ParkingSlotsController).GetMethod(nameof(ParkingSlotsController.UpdateSlot));
        var updateAuth = updateMethod?.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(updateAuth);
        Assert.Equal(Roles.Admin, updateAuth.Roles);

        var patchStatusMethod = typeof(ParkingSlotsController).GetMethod(nameof(ParkingSlotsController.UpdateSlotPhysicalStatus));
        var patchAuth = patchStatusMethod?.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(patchAuth);
        // Under Task 05 Phase 2: Roles.Admin and Roles.Manager with lot scope
        Assert.Equal($"{Roles.Admin},{Roles.Manager}", patchAuth.Roles);

        var deleteMethod = typeof(ParkingSlotsController).GetMethod(nameof(ParkingSlotsController.DeleteSlot));
        var deleteAuth = deleteMethod?.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(deleteAuth);
        Assert.Equal(Roles.Admin, deleteAuth.Roles);

        var getByFloorMethod = typeof(ParkingSlotsController).GetMethod(nameof(ParkingSlotsController.GetSlotsByFloorId));
        var getByFloorAllowAnon = getByFloorMethod?.GetCustomAttribute<AllowAnonymousAttribute>();
        Assert.NotNull(getByFloorAllowAnon);

        var getByIdMethod = typeof(ParkingSlotsController).GetMethod(nameof(ParkingSlotsController.GetSlotById));
        var getByIdAllowAnon = getByIdMethod?.GetCustomAttribute<AllowAnonymousAttribute>();
        Assert.NotNull(getByIdAllowAnon);
    }

    #endregion
}
