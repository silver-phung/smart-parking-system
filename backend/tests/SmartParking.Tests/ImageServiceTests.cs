using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartParking.Application.Common.Exceptions;
using SmartParking.Application.Common.Options;
using SmartParking.Domain.Entities;
using SmartParking.Infrastructure.Services;

namespace SmartParking.Tests;

public class ImageServiceTests
{
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
    public async Task UploadAsync_ReportsMissingCloudinaryConfiguration()
    {
        await using var db = await TestDbHelper.CreateInMemoryDbContextAsync();
        var service = CreateService(db, maxUploadSizeBytes: 16);
        var pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        await Assert.ThrowsAsync<ImageConfigurationException>(() =>
            service.UploadAsync(new MemoryStream(pngHeader), "image/png", pngHeader.Length, 7, CancellationToken.None));
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
            NullLogger<ImageService>.Instance);
}
