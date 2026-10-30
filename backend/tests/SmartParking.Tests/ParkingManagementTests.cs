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
        Assert.Equal(0, lot.TotalCapacity);
        Assert.Equal(200, lot.ConfiguredCapacity);
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

    [Fact]
    public async Task GetLots_WhenLotHasPhysicalSlots_ReturnsAuthoritativePhysicalSlotCapacity()
    {
        var (service, context) = await CreateServiceAsync();
        var lot = new ParkingLot
        {
            Name = "Bãi 1 Chỗ",
            TotalCapacity = 10, // Nominal capacity in DB
            IsActive = true
        };
        context.ParkingLots.Add(lot);
        await context.SaveChangesAsync();

        var floor = new ParkingFloor
        {
            LotId = lot.Id,
            FloorNumber = 1,
            Name = "Tầng G",
            TotalSlots = 1,
            IsActive = true
        };
        context.ParkingFloors.Add(floor);
        await context.SaveChangesAsync();

        var slot = new ParkingSlot
        {
            FloorId = floor.Id,
            SlotCode = "A01",
            PhysicalStatus = SlotPhysicalStatuses.Empty,
            IsActive = true
        };
        context.ParkingSlots.Add(slot);
        await context.SaveChangesAsync();

        var result = await service.GetLotsAsync(new ParkingLotFilterDto { PageNumber = 1, PageSize = 10 }, isAdminOrStaff: true);
        var lotDto = result.Items.FirstOrDefault(l => l.Id == lot.Id);

        Assert.NotNull(lotDto);
        // Authoritative capacity must derive from physical slots (1), NOT nominal 10
        Assert.Equal(1, lotDto.TotalCapacity);
        Assert.Equal(1, lotDto.PhysicalSlotsCount);
        Assert.Equal(10, lotDto.ConfiguredCapacity);
        Assert.Equal(1, lotDto.AvailableSlotsCount);
    }

    [Fact]
    public async Task GetLotById_WhenLotHasPhysicalSlots_ReturnsAuthoritativePhysicalSlotCapacity()
    {
        var (service, context) = await CreateServiceAsync();
        var lot = new ParkingLot
        {
            Name = "Bãi Chi Tiết 1 Chỗ",
            TotalCapacity = 50, // Nominal capacity in DB
            IsActive = true
        };
        context.ParkingLots.Add(lot);
        await context.SaveChangesAsync();

        var floor = new ParkingFloor
        {
            LotId = lot.Id,
            FloorNumber = 1,
            Name = "Tầng 1",
            TotalSlots = 1,
            IsActive = true
        };
        context.ParkingFloors.Add(floor);
        await context.SaveChangesAsync();

        var slot = new ParkingSlot
        {
            FloorId = floor.Id,
            SlotCode = "S01",
            PhysicalStatus = SlotPhysicalStatuses.Empty,
            IsActive = true
        };
        context.ParkingSlots.Add(slot);
        await context.SaveChangesAsync();

        var detailDto = await service.GetLotByIdAsync(lot.Id, isAdminOrStaff: true);

        Assert.NotNull(detailDto);
        Assert.Equal(1, detailDto.TotalCapacity);
        Assert.Equal(1, detailDto.PhysicalSlotsCount);
        Assert.Equal(50, detailDto.ConfiguredCapacity);
        Assert.Equal(1, detailDto.AvailableSlotsCount);
    }

    [Fact]
    public async Task GetLots_MultiLotScenario_OneSlotAndZeroSlot100000_VerifiesExactCapacitiesAndConsistency()
    {
        var (service, context) = await CreateServiceAsync();

        // Lot 1: ConfiguredCapacity 10, 1 physical slot
        var lot1 = new ParkingLot
        {
            Id = 2,
            Name = "Lot 1",
            TotalCapacity = 10,
            IsActive = true
        };
        // Lot 2: ConfiguredCapacity 100000, 0 physical slots
        var lot2 = new ParkingLot
        {
            Id = 4,
            Name = "Lot 2",
            TotalCapacity = 100000,
            IsActive = true
        };
        context.ParkingLots.AddRange(lot1, lot2);
        await context.SaveChangesAsync();

        var floor = new ParkingFloor
        {
            Id = 3,
            LotId = lot1.Id,
            FloorNumber = 100,
            Name = "G",
            TotalSlots = 1,
            IsActive = true
        };
        context.ParkingFloors.Add(floor);
        await context.SaveChangesAsync();

        var slot = new ParkingSlot
        {
            Id = 2,
            FloorId = floor.Id,
            SlotCode = "A01",
            PhysicalStatus = SlotPhysicalStatuses.Empty,
            IsActive = true
        };
        context.ParkingSlots.Add(slot);
        await context.SaveChangesAsync();

        var result = await service.GetLotsAsync(new ParkingLotFilterDto { PageNumber = 1, PageSize = 10 }, isAdminOrStaff: true);

        var lot1Dto = result.Items.FirstOrDefault(l => l.Id == 2);
        var lot2Dto = result.Items.FirstOrDefault(l => l.Id == 4);

        Assert.NotNull(lot1Dto);
        Assert.NotNull(lot2Dto);

        // Lot 1: Authoritative capacity derives from physical slot (1)
        Assert.Equal(1, lot1Dto.TotalCapacity);
        Assert.Equal(1, lot1Dto.PhysicalSlotsCount);
        Assert.Equal(10, lot1Dto.ConfiguredCapacity);
        Assert.Equal(1, lot1Dto.AvailableSlotsCount);
        Assert.Equal(0, lot1Dto.OccupiedSlotsCount);

        // Lot 2: 0 physical slots -> operational capacity is strictly 0 (never authorizes 100000)
        Assert.Equal(0, lot2Dto.TotalCapacity);
        Assert.Equal(0, lot2Dto.PhysicalSlotsCount);
        Assert.Equal(100000, lot2Dto.ConfiguredCapacity);
        Assert.Equal(0, lot2Dto.AvailableSlotsCount);
        Assert.Equal(0, lot2Dto.OccupiedSlotsCount);

        // 1. Dashboard contract verification: "Tổng số chỗ đã triển khai" strictly reflects physical slots
        var activeLots = result.Items.Where(l => l.IsActive).ToList();
        var deployedPhysicalCapacity = activeLots.Sum(l => l.TotalCapacity);
        var configuredDesignCapacity = activeLots.Sum(l => l.ConfiguredCapacity);
        var deployedAvailableSlots = activeLots.Sum(l => l.AvailableSlotsCount);

        Assert.Equal(1, deployedPhysicalCapacity);       // Deployed physical spots = 1 (NOT 100001)
        Assert.Equal(100010, configuredDesignCapacity);   // Nominal design capacity = 100010
        Assert.Equal(1, deployedAvailableSlots);         // Available deployed spots = 1

        // Verify mathematical consistency with Booking/Check-in capacity engine
        var calc1 = await SmartParking.Infrastructure.Common.ParkingLotCapacityHelper.CalculateAsync(context, lot1.Id, lot1);
        Assert.Equal(lot1Dto.TotalCapacity, calc1.TotalCapacity);
        Assert.Equal(lot1Dto.AvailableSlotsCount, calc1.AvailableCapacity);

        var calc2 = await SmartParking.Infrastructure.Common.ParkingLotCapacityHelper.CalculateAsync(context, lot2.Id, lot2);
        Assert.Equal(lot2Dto.TotalCapacity, calc2.TotalCapacity);
        Assert.Equal(lot2Dto.AvailableSlotsCount, calc2.AvailableCapacity);
        Assert.Equal(100000, calc2.ConfiguredCapacity);

        // 2. Deactivation contract: Deactivating Lot 4 completely excludes it from active Dashboard totals & public API
        var (statusSuccess, _, _) = await service.UpdateLotStatusAsync(lot2.Id, new UpdateLotStatusRequestDto { IsActive = false }, 1);
        Assert.True(statusSuccess);

        var resultAfterDeactivate = await service.GetLotsAsync(new ParkingLotFilterDto { PageNumber = 1, PageSize = 10 }, isAdminOrStaff: true);
        var activeLotsAfterDeactivate = resultAfterDeactivate.Items.Where(l => l.IsActive).ToList();

        Assert.Single(activeLotsAfterDeactivate);
        Assert.Equal(1, activeLotsAfterDeactivate.Sum(l => l.PhysicalSlotsCount));
        Assert.Equal(10, activeLotsAfterDeactivate.Sum(l => l.ConfiguredCapacity));   // 100000 excluded!
        Assert.Equal(1, activeLotsAfterDeactivate.Sum(l => l.TotalCapacity));        // 100000 excluded!
        Assert.Equal(1, activeLotsAfterDeactivate.Sum(l => l.AvailableSlotsCount));

        // Public API completely hides deactivated Lot 4
        var publicResult = await service.GetLotsAsync(new ParkingLotFilterDto { PageNumber = 1, PageSize = 10 }, isAdminOrStaff: false);
        Assert.Single(publicResult.Items);
        Assert.Equal(2, publicResult.Items[0].Id);
        Assert.DoesNotContain(publicResult.Items, l => l.Id == 4);

        // 3. DELETE API verification:
        // Lot 1 contains floors/slots -> DELETE is rejected
        var (deleteLot1Success, deleteLot1Msg) = await service.DeleteLotAsync(lot1.Id, 1);
        Assert.False(deleteLot1Success);
        Assert.Contains("Không thể xóa bãi đỗ xe đang chứa các tầng", deleteLot1Msg);

        // Lot 2 (Lot 4 unconfigured) has 0 floors and 0 sessions -> DELETE API exists and succeeds
        var (deleteLot2Success, deleteLot2Msg) = await service.DeleteLotAsync(lot2.Id, 1);
        Assert.True(deleteLot2Success);
        Assert.Contains("Xóa bãi đỗ xe thành công", deleteLot2Msg);

        var getDeleted = await service.GetLotByIdAsync(lot2.Id, isAdminOrStaff: true);
        Assert.Null(getDeleted);
    }

    [Fact]
    public async Task DeactivationSafety_FloorAndSlotWithActiveSession_RejectsDeactivation()
    {
        var (service, context) = await CreateServiceAsync();

        var lot = new ParkingLot { Id = 10, Name = "Safety Lot", TotalCapacity = 5, IsActive = true };
        context.ParkingLots.Add(lot);
        await context.SaveChangesAsync();

        var floor = new ParkingFloor { Id = 20, LotId = lot.Id, FloorNumber = 1, Name = "Tầng 1", TotalSlots = 1, IsActive = true };
        context.ParkingFloors.Add(floor);
        await context.SaveChangesAsync();

        var slot = new ParkingSlot { Id = 30, FloorId = floor.Id, SlotCode = "S-01", PhysicalStatus = SlotPhysicalStatuses.Occupied, IsActive = true };
        context.ParkingSlots.Add(slot);
        await context.SaveChangesAsync();

        var session = new ParkingSession
        {
            LotId = lot.Id,
            PlateNumber = "51A-OCCUPIED",
            CurrentSlotId = slot.Id,
            Status = SessionStatuses.Active,
            CheckInTime = DateTime.UtcNow
        };
        context.ParkingSessions.Add(session);
        await context.SaveChangesAsync();

        // 1. Unsafe slot deactivation is rejected
        var (slotDeactSuccess, slotDeactMsg, _) = await service.UpdateSlotAsync(slot.Id, new UpdateParkingSlotRequestDto { SlotCode = "S-01", IsActive = false }, 1);
        Assert.False(slotDeactSuccess);
        Assert.Contains("Không thể vô hiệu hóa ô đỗ khi đang có xe đỗ", slotDeactMsg);

        // 2. Unsafe floor deactivation is rejected
        var (floorDeactSuccess, floorDeactMsg, _) = await service.UpdateFloorAsync(floor.Id, new UpdateParkingFloorRequestDto { FloorNumber = 1, Name = "Tầng 1", TotalSlots = 1, IsActive = false }, 1);
        Assert.False(floorDeactSuccess);
        Assert.Contains("Không thể vô hiệu hóa tầng khi đang có xe đỗ", floorDeactMsg);

        // 3. Unsafe slot physical status to MAINTENANCE is rejected while session is active
        var (maintSuccess, maintMsg, _) = await service.UpdateSlotPhysicalStatusAsync(slot.Id, new UpdateSlotPhysicalStatusRequestDto { PhysicalStatus = SlotPhysicalStatuses.Maintenance }, 1, Roles.Admin, "127.0.0.1");
        Assert.False(maintSuccess);
        Assert.Contains("Không thể chuyển trạng thái ô đỗ đang có xe", maintMsg);
    }

    #endregion
}
