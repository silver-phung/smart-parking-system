using SmartParking.Application.DTOs.Images;

namespace SmartParking.Application.Common.Interfaces;

public interface IImageService
{
    Task<StoredImageDto> UploadAsync(
        Stream content,
        string contentType,
        long contentLength,
        long userId,
        CancellationToken cancellationToken);

    Task<StoredImageDto?> GetByIdAsync(long id, long userId, bool isAdmin, CancellationToken cancellationToken);

    Task<ImageDeleteResult> DeleteAsync(long id, long userId, bool isAdmin, CancellationToken cancellationToken);
}

public enum ImageDeleteResult
{
    Deleted,
    NotFound,
    Forbidden
}
