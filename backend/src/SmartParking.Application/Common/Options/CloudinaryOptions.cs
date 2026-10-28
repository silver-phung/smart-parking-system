namespace SmartParking.Application.Common.Options;

public sealed class CloudinaryOptions
{
    public const string SectionName = "Cloudinary";
    public string? Url { get; set; }
    public string? CloudName { get; set; }
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
    public string Folder { get; set; } = "smart-parking/images";
    public long MaxUploadSizeBytes { get; set; } = 10 * 1024 * 1024;
}
