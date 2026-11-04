using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SmartParking.Application.Common.Exceptions;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.DTOs.StaffAssignments;

namespace SmartParking.Infrastructure.Services;

public sealed class RecognitionServiceClient(HttpClient client) : IStaffDetectionService, IRecognitionEvidenceReader
{
    public const string KeyHeader = "X-Recognition-Service-Key";

    public async Task<StaffDetectionDto> DetectAsync(
        Stream image, string contentType, long contentLength, long staffUserId, CancellationToken cancellationToken)
    {
        if (contentLength <= 0 || !MediaTypeHeaderValue.TryParse(contentType, out var mediaType))
            throw new ImageValidationException("A non-empty image and a valid content type are required.");

        using var form = new MultipartFormDataContent();
        var content = new StreamContent(image);
        content.Headers.ContentType = mediaType;
        form.Add(content, "file", "staff-upload");
        var request = new HttpRequestMessage(HttpMethod.Post, $"internal/recognition/detect?staffUserId={staffUserId}")
        {
            Content = form
        };
        return (await SendAsync<StaffDetectionDto>(request, allowNotFound: false, cancellationToken))!;
    }

    public Task<StaffDetectionDto?> ConfirmAsync(
        long recognitionId, string plateNumber, string? note, long staffUserId, CancellationToken cancellationToken) =>
        SendAsync<StaffDetectionDto>(new HttpRequestMessage(HttpMethod.Post, $"internal/recognition/{recognitionId}/confirm")
        {
            Content = JsonContent.Create(new ConfirmRecognition(plateNumber, note, staffUserId))
        }, allowNotFound: true, cancellationToken);

    public async Task<RecognitionEvidence?> FindAsync(long id, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            return await SendAsync<RecognitionEvidence>(
                new HttpRequestMessage(HttpMethod.Get, $"internal/recognition/{id}/evidence"),
                allowNotFound: true, requestCancellation.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw Unavailable();
        }
    }

    private async Task<T?> SendAsync<T>(HttpRequestMessage request, bool allowNotFound, CancellationToken ct) where T : class
    {
        using (request)
        {
            try
            {
                using var response = await client.SendAsync(request, ct);
                if (response.StatusCode == HttpStatusCode.NotFound && allowNotFound)
                    return null;
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                        throw Unavailable();
                    var error = await response.Content.ReadFromJsonAsync<RecognitionError>(cancellationToken: ct);
                    var message = error?.Message ?? "Recognition service rejected the request.";
                    if (response.StatusCode == HttpStatusCode.BadRequest)
                        throw new ImageValidationException(message);
                    throw new AiServiceException(response.StatusCode, message);
                }

                return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct) ?? throw Unavailable();
            }
            catch (HttpRequestException) { throw Unavailable(); }
            catch (JsonException) { throw Unavailable(); }
            catch (NotSupportedException) { throw Unavailable(); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw Unavailable(); }
        }
    }

    private static AiServiceException Unavailable() =>
        new(HttpStatusCode.ServiceUnavailable, "Recognition service is unavailable.");

    public sealed record ConfirmRecognition(string PlateNumber, string? Note, long StaffUserId);
    private sealed record RecognitionError(string Message);
}
