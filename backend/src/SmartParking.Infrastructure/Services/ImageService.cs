using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using SmartParking.Application.Common.Exceptions;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Common.Options;
using SmartParking.Application.DTOs.Images;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Services;

public sealed class ImageService : IImageService
{
    private static readonly IReadOnlyDictionary<string, string> SupportedContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = "jpg",
            ["image/png"] = "png",
            ["image/webp"] = "webp"
        };

    private readonly IApplicationDbContext _dbContext;
    private readonly CloudinaryOptions _options;
    private readonly ILogger<ImageService> _logger;
    private readonly IParkingAuthorizationService _authorization;

    public ImageService(
        IApplicationDbContext dbContext,
        IOptions<CloudinaryOptions> options,
        ILogger<ImageService> logger,
        IParkingAuthorizationService authorization)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
        _authorization = authorization;
    }

    public async Task<StoredImageDto> UploadAsync(
        Stream content,
        string contentType,
        long contentLength,
        long userId,
        CancellationToken cancellationToken)
    {
        if (_options.MaxUploadSizeBytes <= 0)
            throw new ImageConfigurationException("The image upload size limit must be greater than zero.");

        if (contentLength <= 0 || contentLength > _options.MaxUploadSizeBytes)
            throw new ImageValidationException("Image file is empty or exceeds the configured size limit.");

        if (!SupportedContentTypes.TryGetValue(contentType, out var expectedFormat))
            throw new ImageValidationException("Only JPEG, PNG, and WebP images are supported.");

        await using var imageStream = new MemoryStream();
        var buffer = new byte[81920];
        int bytesRead;
        while ((bytesRead = await content.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (imageStream.Length + bytesRead > _options.MaxUploadSizeBytes)
                throw new ImageValidationException("Image file exceeds the configured size limit.");

            await imageStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }

        var imageContent = imageStream.ToArray();
        if (imageContent.Length == 0 || !HasExpectedSignature(imageContent, contentType))
            throw new ImageValidationException("The file content does not match a supported image format.");

        var imageKey = Guid.NewGuid().ToString("N");
        var storedImage = new StoredImage
        {
            UserId = userId,
            ContentSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(imageContent)),
            PublicId = imageKey,
            Url = $"/api/v1/images/content/{imageKey}",
            Format = expectedFormat,
            Content = imageContent,
            Bytes = imageStream.Length,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.StoredImages.Add(storedImage);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is DbUpdateException or NpgsqlException)
        {
            _logger.LogError(exception, "Unable to save image metadata and content.");
            throw new ImagePersistenceException();
        }

        return ToDto(storedImage);
    }

    public async Task<StoredImageDto?> GetByIdAsync(
        long id,
        long userId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var image = await _dbContext.StoredImages
            .AsNoTracking()
            .FirstOrDefaultAsync(image => image.Id == id && (isAdmin || image.UserId == userId), cancellationToken);

        return image is null ? null : ToDto(image);
    }

    public async Task<StoredImageContentDto?> GetContentAsync(
        string publicId, long userId, string role, CancellationToken cancellationToken)
    {
        var image = await _dbContext.StoredImages.AsNoTracking()
            .FirstOrDefaultAsync(i => i.PublicId == publicId && i.Content != null, cancellationToken);
        if (image is null) return null;

        var allowed = image.UserId == userId || role == SmartParking.Domain.Constants.Roles.Admin;
        if (!allowed)
        {
            var sessions = await _dbContext.ParkingSessions.AsNoTracking()
                .Where(s => s.EntryImageId == image.Id || s.CheckInImageUrl == image.Url || s.CheckOutImageUrl == image.Url)
                .Select(s => new { s.LotId, VehicleUserId = s.Vehicle == null ? null : s.Vehicle.UserId })
                .ToListAsync(cancellationToken);
            foreach (var session in sessions)
            {
                if (session.VehicleUserId == userId ||
                    await _authorization.HasLotAccessAsync(userId, role, session.LotId, cancellationToken))
                {
                    allowed = true;
                    break;
                }
            }
        }
        if (!allowed) return null;
        var contentType = image.Format switch
        {
            "jpg" => "image/jpeg",
            "png" => "image/png",
            "webp" => "image/webp",
            _ => throw new ImagePersistenceException()
        };
        return new StoredImageContentDto(image.Content!, contentType);
    }

    public async Task<ImageDeleteResult> DeleteAsync(
        long id,
        long userId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var image = await _dbContext.StoredImages
            .FirstOrDefaultAsync(image => image.Id == id, cancellationToken);

        if (image is null)
            return ImageDeleteResult.NotFound;

        if (!isAdmin && image.UserId != userId)
            return ImageDeleteResult.Forbidden;

        // Preserve linked evidence before making any destructive call to Cloudinary.
        if (await _dbContext.ParkingSessions.AnyAsync(s => s.EntryImageId == id || s.CheckInImageUrl == image.Url || s.CheckOutImageUrl == image.Url, cancellationToken))
            return ImageDeleteResult.Forbidden;
        if (image.Content is not null)
        {
            _dbContext.StoredImages.Remove(image);
            await SaveDeletedMetadataAsync(image.Id, cancellationToken);
            return ImageDeleteResult.Deleted;
        }

        var cloudinary = CreateCloudinaryClient();
        DeletionResult deletion;
        try
        {
            deletion = await cloudinary.DestroyAsync(new DeletionParams(image.PublicId)
            {
                ResourceType = ResourceType.Image
            });
        }
        catch (HttpRequestException)
        {
            throw new ImageProviderException();
        }

        if (deletion.Error is not null)
            throw new ImageProviderException();

        if (string.Equals(deletion.Result, "not found", StringComparison.OrdinalIgnoreCase))
        {
            _dbContext.StoredImages.Remove(image);
            await SaveDeletedMetadataAsync(image.Id, cancellationToken);
            return ImageDeleteResult.Deleted;
        }

        if (!string.Equals(deletion.Result, "ok", StringComparison.OrdinalIgnoreCase))
            throw new ImageProviderException();

        _dbContext.StoredImages.Remove(image);
        await SaveDeletedMetadataAsync(image.Id, cancellationToken);
        return ImageDeleteResult.Deleted;
    }

    private Cloudinary CreateCloudinaryClient()
    {
        var url = _options.Url;
        if (!string.IsNullOrWhiteSpace(url))
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || !string.Equals(uri.Scheme, "cloudinary", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(uri.UserInfo)
                || uri.UserInfo.IndexOf(':') <= 0
                || uri.UserInfo.EndsWith(':')
                || string.IsNullOrWhiteSpace(uri.Host))
            {
                throw new ImageConfigurationException();
            }

            return new Cloudinary(url);
        }

        if (string.IsNullOrWhiteSpace(_options.CloudName)
            || string.IsNullOrWhiteSpace(_options.ApiKey)
            || string.IsNullOrWhiteSpace(_options.ApiSecret))
            throw new ImageConfigurationException();

        return new Cloudinary(new Account(_options.CloudName, _options.ApiKey, _options.ApiSecret));
    }

    private async Task SaveDeletedMetadataAsync(long imageId, CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is DbUpdateException or NpgsqlException)
        {
            _logger.LogError(exception, "Could not remove metadata for image {ImageId}.", imageId);
            throw new ImagePersistenceException();
        }
    }

    private static bool HasExpectedSignature(byte[] bytes, string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            "image/png" => bytes.Length >= 8
                && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            "image/webp" => bytes.Length >= 12
                && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8)
                && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
    }

    private static StoredImageDto ToDto(StoredImage image) =>
        new(image.Id, image.Url, image.Format, image.Bytes, image.Width, image.Height, image.CreatedAt);
}
