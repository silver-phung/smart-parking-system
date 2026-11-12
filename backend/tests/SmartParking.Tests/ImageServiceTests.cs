using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartParking.Application.Common.Exceptions;
using SmartParking.Application.Common.Options;
using SmartParking.Domain.Constants;
using SmartParking.Domain.Entities;
using SmartParking.Infrastructure.Services;

namespace SmartParking.Tests;

public class ImageServiceTests
{
    [Fact]
    public async Task TicketEvidenceImageIsReadableByAssignedStaffAndCannotBeDeleted()
    {
        await using var db = await TestDbHelper.CreateInMemoryDbContextAsync();
        db.Users.Add(new User { Id = 8, RoleId = 3, Username = "staff_evidence", Email = "staff_evidence@test", FullName = "Staff", PasswordHash = "x" });
        db.StaffLotAssignments.Add(new StaffLotAssignment { UserId = 8, LotId = 1, IsActive = true });
        var service = CreateService(db, 16);
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        var image = await service.UploadAsync(new MemoryStream(png), "image/png", png.Length, 7, CancellationToken.None);
        db.ParkingSessions.Add(new ParkingSession { LotId = 1, EntryImageId = image.Id });
        await db.SaveChangesAsync();
        Assert.NotNull(await service.GetContentAsync(image.Url.Split('/').Last(), 8, Roles.Staff, CancellationToken.None));
        Assert.Equal(SmartParking.Application.Common.Interfaces.ImageDeleteResult.Forbidden,
            await service.DeleteAsync(image.Id, 7, true, CancellationToken.None));
    }
    [Fact]
    public async Task UploadAsync_RejectsUnsupportedContentType()
    {
        await using var db = await TestDbHelper.CreateInMemoryDbContextAsync();
        var service = CreateService(db, maxUploadSizeBytes: 16);

        await Assert.ThrowsAsync<ImageValidationException>(() =>
            service.UploadAsync(new MemoryStream([1, 2, 3]), "image/gif", 3, 7, CancellationToken.None));
    }

    [Fact]
    public async Task UploadAsync_RejectsMismatchedImageSignature()
    {
        await using var db = await TestDbHelper.CreateInMemoryDbContextAsync();
        var service = CreateService(db, maxUploadSizeBytes: 16);

        await Assert.ThrowsAsync<ImageValidationException>(() =>
            service.UploadAsync(new MemoryStream([1, 2, 3]), "image/png", 3, 7, CancellationToken.None));
    }

    [Fact]
    public async Task UploadAsync_RejectsFilesExceedingConfiguredSize()
    {
        await using var db = await TestDbHelper.CreateInMemoryDbContextAsync();
        var service = CreateService(db, maxUploadSizeBytes: 8);

        await Assert.ThrowsAsync<ImageValidationException>(() =>
            service.UploadAsync(new MemoryStream(new byte[9]), "image/png", 9, 7, CancellationToken.None));
    }

    [Fact]
    public async Task UploadAsync_StoresImageBytesWithoutCloudinary()
    {
        await using var db = await TestDbHelper.CreateInMemoryDbContextAsync();
        var service = CreateService(db, maxUploadSizeBytes: 16);
        var pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        var uploaded = await service.UploadAsync(new MemoryStream(pngHeader), "image/png", pngHeader.Length, 7, CancellationToken.None);
        var key = uploaded.Url.Split('/').Last();
        var content = await service.GetContentAsync(key, 7, Roles.Staff, CancellationToken.None);
        Assert.NotNull(content);
        Assert.Equal(pngHeader, content.Content);
        Assert.Equal("image/png", content.ContentType);
        Assert.Null(uploaded.Width);
        Assert.Null(uploaded.Height);
        Assert.Null(await service.GetContentAsync(key, 8, Roles.User, CancellationToken.None));
        Assert.NotNull(await service.GetContentAsync(key, 8, Roles.Admin, CancellationToken.None));
        Assert.Equal(SmartParking.Application.Common.Interfaces.ImageDeleteResult.Forbidden,
            await service.DeleteAsync(uploaded.Id, 8, false, CancellationToken.None));
        Assert.Equal(SmartParking.Application.Common.Interfaces.ImageDeleteResult.Deleted,
            await service.DeleteAsync(uploaded.Id, 7, false, CancellationToken.None));
        Assert.Null(await service.GetContentAsync(key, 7, Roles.Staff, CancellationToken.None));
    }

    [Fact]
    public async Task GetContentAsync_AllowsAssignedLotStaffAndVehicleOwnerOnly()
    {
        await using var db = await TestDbHelper.CreateInMemoryDbContextAsync();
        db.Users.AddRange(
            new User { Id = 8, RoleId = 3, IsActive = true, Username = "staff", Email = "staff@test", FullName = "Staff", PasswordHash = "x" },
            new User { Id = 9, RoleId = 3, IsActive = true, Username = "other", Email = "other@test", FullName = "Other", PasswordHash = "x" });
        db.StaffLotAssignments.Add(new StaffLotAssignment { UserId = 8, LotId = 1, IsActive = true });
        db.Vehicles.Add(new Vehicle { Id = 12, UserId = 10, PlateNumber = "51A12345" });
        var service = CreateService(db, 16);
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        var uploaded = await service.UploadAsync(new MemoryStream(png), "image/png", png.Length, 7, CancellationToken.None);
        db.ParkingSessions.Add(new ParkingSession { LotId = 1, VehicleId = 12, CheckInImageUrl = uploaded.Url });
        await db.SaveChangesAsync();
        var key = uploaded.Url.Split('/').Last();

        Assert.NotNull(await service.GetContentAsync(key, 8, Roles.Staff, CancellationToken.None));
        Assert.NotNull(await service.GetContentAsync(key, 10, Roles.User, CancellationToken.None));
        Assert.Null(await service.GetContentAsync(key, 9, Roles.Staff, CancellationToken.None));
        Assert.Null(await service.GetContentAsync(key, 11, Roles.User, CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdAsync_RestrictsMetadataToOwnerUnlessAdmin()
    {
        await using var db = await TestDbHelper.CreateInMemoryDbContextAsync();
        db.StoredImages.Add(new StoredImage
        {
            Id = 11,
            UserId = 7,
            PublicId = "smart-parking/images/sample",
            Url = "https://res.cloudinary.com/example/image/upload/sample.png",
            Format = "png",
            Bytes = 100,
            Width = 10,
            Height = 10,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, maxUploadSizeBytes: 16);

        Assert.NotNull(await service.GetByIdAsync(11, 7, false, CancellationToken.None));
        Assert.Null(await service.GetByIdAsync(11, 8, false, CancellationToken.None));
        Assert.NotNull(await service.GetByIdAsync(11, 8, true, CancellationToken.None));
    }

    private static ImageService CreateService(TestSmartParkingDbContext db, long maxUploadSizeBytes) =>
        new(
            db,
            Options.Create(new CloudinaryOptions { MaxUploadSizeBytes = maxUploadSizeBytes }),
            NullLogger<ImageService>.Instance,
            new ParkingAuthorizationService(db));
}
