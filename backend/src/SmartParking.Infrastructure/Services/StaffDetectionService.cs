using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartParking.Application.Common.Exceptions;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.DTOs.StaffAssignments;
using SmartParking.Infrastructure.Data;
using SmartParking.Infrastructure.Entities;

namespace SmartParking.Infrastructure.Services;

public sealed class StaffDetectionService : IStaffDetectionService
{
    private static readonly HashSet<string> SupportedTypes =
        ["image/jpeg", "image/png", "image/webp"];
    private readonly AiRecognitionDbContext _db;
    private readonly HttpClient _client;
    private readonly long _maxBytes;

    public StaffDetectionService(
        AiRecognitionDbContext db,
        HttpClient client,
        IConfiguration configuration)
    {
        _db = db;
        _client = client;
        _maxBytes = configuration.GetValue<long?>("AiService:MaxUploadSizeBytes") ?? 10 * 1024 * 1024;
    }

    public async Task<StaffDetectionDto> DetectAsync(
        Stream image, string contentType, long contentLength, long staffUserId,
        CancellationToken cancellationToken)
    {
        if (!SupportedTypes.Contains(contentType) || contentLength <= 0 || contentLength > _maxBytes)
            throw new ImageValidationException("Only non-empty JPEG, PNG, or WebP images within the size limit are supported.");

        await using var buffer = new MemoryStream();
        await image.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        if (bytes.Length == 0 || bytes.Length > _maxBytes || !HasImageSignature(bytes, contentType))
            throw new ImageValidationException("Image content is invalid, empty, or exceeds the configured size limit.");

        using var form = new MultipartFormDataContent();
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(content, "file", "staff-upload");

        using var response = await _client.PostAsync("api/v1/detect", form, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new AiServiceException(response.StatusCode, ReadMessage(body) ?? "AI service rejected the image.");

        var result = JsonSerializer.Deserialize<AiResponse>(body, JsonOptions);
        if (result is null)
            throw new AiServiceException(HttpStatusCode.UnprocessableEntity,
                "AI service returned an invalid response.");

        var recognized = result.Success && result.Data is not null
            && !string.IsNullOrWhiteSpace(result.Data.PlateNumber)
            && !string.IsNullOrWhiteSpace(result.Data.RawPlateNumber);
        var data = result.Data;

        var record = new AiRecognitionRecord
        {
            StaffUserId = staffUserId,
            PlateNumber = recognized ? data!.PlateNumber : null,
            RawPlateNumber = recognized ? data!.RawPlateNumber : null,
            FailureReason = recognized ? null : result.Message,
            Confidence = data?.Confidence ?? 0,
            IsLegit = data?.IsLegit ?? false,
            VehicleConfidence = data?.VehicleConfidence,
            VehicleBboxJson = Serialize(data?.VehicleBbox),
            PlateBboxJson = Serialize(data?.PlateBbox),
            InferenceTimeMs = data?.InferenceTimeMs ?? 0
        };
        _db.RecognitionRecords.Add(record);
        await _db.SaveChangesAsync(cancellationToken);

        return Map(record, recognized ? "PENDING_CONFIRMATION" : "UNREADABLE");
    }

    public async Task<StaffDetectionDto?> ConfirmAsync(
        long recognitionId, string plateNumber, string? note, long staffUserId,
        CancellationToken cancellationToken)
    {
        var record = await _db.RecognitionRecords
            .FirstOrDefaultAsync(x => x.Id == recognitionId, cancellationToken);
        if (record is null)
            return null;

        if (string.IsNullOrWhiteSpace(note) && record.PlateNumber is not null
            && !string.Equals(record.PlateNumber, plateNumber, StringComparison.OrdinalIgnoreCase))
            throw new ImageValidationException("A note explaining a manual correction is required.");

        record.ConfirmedPlateNumber = plateNumber;
        record.ConfirmedByStaffUserId = staffUserId;
        record.ConfirmedAt = DateTime.UtcNow;
        record.ConfirmationNote = string.IsNullOrWhiteSpace(note) ? "Staff confirmed recognized plate." : note;
        await _db.SaveChangesAsync(cancellationToken);

        return Map(record, "CONFIRMED");
    }

    private static StaffDetectionDto Map(AiRecognitionRecord record, string status) =>
        new(record.Id, record.StaffUserId, record.PlateNumber, record.RawPlateNumber,
            status, record.FailureReason, record.Confidence, record.IsLegit, record.VehicleConfidence,
            Deserialize(record.VehicleBboxJson), Deserialize(record.PlateBboxJson), record.InferenceTimeMs,
            record.CreatedAt, record.ConfirmedPlateNumber, record.ConfirmedByStaffUserId,
            record.ConfirmedAt, record.ConfirmationNote);

    private static string? Serialize(IReadOnlyList<int>? value) =>
        value is null ? null : JsonSerializer.Serialize(value);

    private static IReadOnlyList<int>? Deserialize(string? value) =>
        value is null ? null : JsonSerializer.Deserialize<int[]>(value);

    private static bool HasImageSignature(byte[] bytes, string contentType)
    {
        if (contentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase))
            return bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
        if (contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase))
            return bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(
                new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        return contentType.Equals("image/webp", StringComparison.OrdinalIgnoreCase)
            && bytes.Length >= 12
            && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8)
            && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8);
    }

    private static string? ReadMessage(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("message", out var message))
                return message.GetString();
            if (json.RootElement.TryGetProperty("detail", out var detail))
                return detail.ValueKind == JsonValueKind.String ? detail.GetString() : detail.ToString();
            return null;
        }
        catch (JsonException) { return null; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private sealed record AiResponse(bool Success, string Message, AiData? Data);
    private sealed record AiData(
        string PlateNumber, string RawPlateNumber, double Confidence, bool IsLegit,
        double? VehicleConfidence, IReadOnlyList<int>? VehicleBbox,
        IReadOnlyList<int>? PlateBbox, double InferenceTimeMs);
}

public sealed class AiServiceException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
