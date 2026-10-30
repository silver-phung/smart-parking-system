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

    public ImageService(
        IApplicationDbContext dbContext,
        IOptions<CloudinaryOptions> options,
        ILogger<ImageService> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<StoredImageDto> UploadAsync(
        Stream content,
        string contentType,
        long contentLength,
        long userId,
        CancellationToken cancellationToken)
    {
        if (_options.MaxUploadSizeBytes <= 0)
            throw new ImageConfigurationException();

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

        if (imageStream.Length == 0 || !HasExpectedSignature(imageStream.GetBuffer(), contentType))
            throw new ImageValidationException("The file content does not match a supported image format.");

        imageStream.Position = 0;
        var cloudinary = CreateCloudinaryClient();
        ImageUploadResult upload;
        try
        {
            upload = await cloudinary.UploadAsync(new ImageUploadParams
            {
                File = new FileDescription($"upload.{expectedFormat}", imageStream),
                Folder = _options.Folder,
                UseFilename = false,
                UniqueFilename = true
            }, cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new ImageProviderException();
        }

        if (upload.Error is not null || string.IsNullOrWhiteSpace(upload.PublicId) || upload.SecureUrl is null)
            throw new ImageProviderException();

        var storedImage = new StoredImage
        {
            UserId = userId,
            PublicId = upload.PublicId,
            Url = upload.SecureUrl.ToString(),
            Format = upload.Format ?? expectedFormat,
            Bytes = upload.Bytes,
            Width = upload.Width,
            Height = upload.Height,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.StoredImages.Add(storedImage);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is DbUpdateException or NpgsqlException)
        {
            await RemoveOrphanedUploadAsync(cloudinary, storedImage.PublicId);
            throw new ImagePersistenceException();
        }
        catch (OperationCanceledException)
        {
            await RemoveOrphanedUploadAsync(cloudinary, storedImage.PublicId);
            throw;
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

    private async Task RemoveOrphanedUploadAsync(Cloudinary cloudinary, string publicId)
    {
        try
        {
            var deletion = await cloudinary.DestroyAsync(new DeletionParams(publicId)
            {
                ResourceType = ResourceType.Image
            });

            if (deletion.Error is not null || !string.Equals(deletion.Result, "ok", StringComparison.OrdinalIgnoreCase))
                _logger.LogWarning("Cloudinary cleanup failed for uploaded asset {PublicId}.", publicId);
        }
        catch (HttpRequestException)
        {
            _logger.LogWarning("Cloudinary cleanup failed for uploaded asset {PublicId}.", publicId);
        }
    }

    private async Task SaveDeletedMetadataAsync(long imageId, CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is DbUpdateException or NpgsqlException)
        {
            _logger.LogError("Cloudinary asset for image {ImageId} was deleted, but its metadata could not be removed.", imageId);
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
