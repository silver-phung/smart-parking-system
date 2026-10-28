namespace SmartParking.Application.DTOs.Images;

/// <summary>Safe image metadata returned by the image API.</summary>
public sealed record StoredImageDto(
    long Id,
    string Url,
    string Format,
    long Bytes,
    int Width,
    int Height,
    DateTime CreatedAt);
