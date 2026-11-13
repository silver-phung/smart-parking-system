using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartParking.Application.Common.Exceptions;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.DTOs.StaffAssignments;
using SmartParking.Application.Common.Helpers;
using SmartParking.Domain.Entities;
using SmartParking.Persistence.Data;
using SmartParking.Persistence.Entities;

namespace SmartParking.Infrastructure.Services;

public sealed class StaffDetectionService : IStaffDetectionService
{
    private static readonly HashSet<string> SupportedTypes =
        ["image/jpeg", "image/png", "image/webp"];
    private readonly AiRecognitionDbContext _db;
    private readonly IApplicationDbContext _parkingDb;
    private readonly HttpClient _client;
    private readonly long _maxBytes;

    public StaffDetectionService(
        AiRecognitionDbContext db,
        IApplicationDbContext parkingDb,
        HttpClient client,
        IConfiguration configuration)
    {
        _db = db;
        _parkingDb = parkingDb;
        _client = client;
        _maxBytes = configuration.GetValue<long?>("AiService:MaxUploadSizeBytes") ?? 10 * 1024 * 1024;
    }

    public async Task<StaffDetectionDto> DetectAsync(
        Stream image, string contentType, long contentLength, long staffUserId, int lotId, string purpose,
        CancellationToken cancellationToken)
    {
        if (purpose == "ENTRY" && !await _parkingDb.ParkingLots.AnyAsync(x => x.Id == lotId && x.IsActive, cancellationToken))
            throw new ImageValidationException("Selected parking lot is unavailable.");
        if (purpose == "EXIT" && !await _parkingDb.ParkingLots.AnyAsync(x => x.Id == lotId, cancellationToken))
            throw new ImageValidationException("Selected parking lot was not found.");
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
            ParkingLotId = lotId,
            Purpose = purpose,
            ContentSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)),
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

        if (purpose == "EXIT" && recognized)
        {
            var candidates = await _parkingDb.ParkingSessions
                .Where(x => x.LotId == lotId && (x.Status == "ACTIVE" || x.Status == "PAID"))
                .Select(x => new { x.Id, x.PlateNumber, x.Status })
                .ToListAsync(cancellationToken);
            var matches = candidates.Where(x => PlateNumberHelper.IsMatch(data!.PlateNumber, x.PlateNumber)).ToList();
            if (matches.Count == 1)
            {
                var match = matches[0];
                record.MatchedParkingSessionId = match.Id;
                record.ConfirmedPlateNumber = match.PlateNumber;
                record.ConfirmationNote = "AI exit plate matched an open session; staff checkout confirmation is still required.";
                await _db.SaveChangesAsync(cancellationToken);
            }
            else if (matches.Count > 1)
            {
                record.FailureReason = "AI plate matched more than one open session; manual review required.";
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        return Map(record, recognized ? "PENDING_CONFIRMATION" : "UNREADABLE");
    }

    public async Task<StaffDetectionDto?> ConfirmAsync(
        long recognitionId, string plateNumber, string? note, long staffUserId, int lotId,
        CancellationToken cancellationToken)
    {
        var record = await _db.RecognitionRecords
            .FirstOrDefaultAsync(x => x.Id == recognitionId, cancellationToken);
        if (record is null)
            return null;

        if (record.Purpose != "ENTRY" || record.ParkingLotId != lotId)
            throw new ImageValidationException("Recognition does not belong to an entry at the selected parking lot.");

        if (record.StaffUserId != staffUserId)
            throw new ImageValidationException("Only the staff member who captured this entry image may confirm it.");

        if (record.ConfirmedAt.HasValue)
        {
            if (!string.Equals(record.ConfirmedPlateNumber, plateNumber, StringComparison.OrdinalIgnoreCase))
                throw new ImageValidationException("This recognition has already been confirmed with another plate.");
            var existingWorkflow = await _parkingDb.StaffVehicleEntries.AnyAsync(x => x.RecognitionId == record.Id, cancellationToken);
            if (!existingWorkflow)
                throw new ImageValidationException("Recognition was confirmed but its staff entry workflow was not created. Capture and confirm a new image.");
            return Map(record, "CONFIRMED");
        }

        if (string.IsNullOrWhiteSpace(record.PlateNumber))
            throw new ImageValidationException("AI could not recognize a plate. Capture a clearer image before opening an entry workflow.");

        if (!await _parkingDb.ParkingLots.AnyAsync(x => x.Id == lotId && x.IsActive, cancellationToken))
            throw new ImageValidationException("Selected parking lot is unavailable.");

        if (string.IsNullOrWhiteSpace(note) && record.PlateNumber is not null
            && !string.Equals(record.PlateNumber, plateNumber, StringComparison.OrdinalIgnoreCase))
            throw new ImageValidationException("A note explaining a manual correction is required.");

        record.ConfirmedPlateNumber = plateNumber;
        record.ConfirmedByStaffUserId = staffUserId;
        record.ConfirmedAt = DateTime.UtcNow;
        record.ConfirmationNote = string.IsNullOrWhiteSpace(note) ? "Staff confirmed recognized plate." : note;
        record.ParkingLotId = lotId;
        record.Purpose = "ENTRY";

        var open = await _parkingDb.StaffVehicleEntries.AnyAsync(x =>
            x.LotId == lotId && x.PlateNumber == plateNumber && x.Status == "SEARCHING_FOR_SLOT", cancellationToken);
        if (open)
            throw new ImageValidationException("This vehicle already has an open staff entry workflow at this lot.");

        var entry = new StaffVehicleEntry
        {
            RecognitionId = record.Id,
            LotId = lotId,
            StaffUserId = staffUserId,
            PlateNumber = plateNumber,
            Status = "SEARCHING_FOR_SLOT"
        };
        _parkingDb.StaffVehicleEntries.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);
        await _parkingDb.SaveChangesAsync(cancellationToken);

        return Map(record, "CONFIRMED");
    }

    public async Task<StaffDetectionDto?> ConfirmExitAsync(
        long recognitionId, long sessionId, string plateNumber, string? note,
        long staffUserId, int lotId, CancellationToken cancellationToken)
    {
        var record = await _db.RecognitionRecords
            .FirstOrDefaultAsync(x => x.Id == recognitionId, cancellationToken);
        if (record is null) return null;
        if (record.Purpose != "EXIT" || record.ParkingLotId != lotId || record.StaffUserId != staffUserId)
            throw new ImageValidationException("Exit recognition does not belong to this staff member and parking lot.");
        if (record.ConfirmedAt.HasValue && record.MatchedParkingSessionId != sessionId)
            throw new ImageValidationException("This exit recognition has already been confirmed for another parking session.");
        var session = await _parkingDb.ParkingSessions.FirstOrDefaultAsync(x =>
            x.Id == sessionId && x.LotId == lotId && (x.Status == "ACTIVE" || x.Status == "PAID"), cancellationToken);
        if (session is null)
            throw new ImageValidationException("Open parking session was not found at the selected lot.");
        if (!PlateNumberHelper.IsMatch(plateNumber, session.PlateNumber))
            throw new ImageValidationException("Staff confirmed plate does not match the selected parking session.");
        var aiMatchesPlate = !string.IsNullOrWhiteSpace(record.PlateNumber)
            && PlateNumberHelper.IsMatch(record.PlateNumber, plateNumber);
        if (!aiMatchesPlate && string.IsNullOrWhiteSpace(note))
            throw new ImageValidationException("A note explaining the manual correction is required.");

        record.MatchedParkingSessionId = session.Id;
        record.ConfirmedPlateNumber = session.PlateNumber;
        record.ConfirmedByStaffUserId = staffUserId;
        record.ConfirmedAt = DateTime.UtcNow;
        record.ConfirmationNote = string.IsNullOrWhiteSpace(note)
            ? "Staff confirmed the AI exit recognition against the vehicle and open session."
            : note;
        await _db.SaveChangesAsync(cancellationToken);
        return Map(record, "CONFIRMED");
    }

    private static StaffDetectionDto Map(AiRecognitionRecord record, string status) =>
        new(record.Id, record.StaffUserId, record.PlateNumber, record.RawPlateNumber,
            status, record.FailureReason, record.Confidence, record.IsLegit, record.VehicleConfidence,
            Deserialize(record.VehicleBboxJson), Deserialize(record.PlateBboxJson), record.InferenceTimeMs,
            record.CreatedAt, record.ConfirmedPlateNumber, record.ConfirmedByStaffUserId,
            record.ConfirmedAt, record.ConfirmationNote, record.ParkingLotId, record.Purpose,
            record.MatchedParkingSessionId);

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
