using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.Application.Common.Exceptions;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Infrastructure.Data;
using SmartParking.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("Sprint4Recognition");
if (string.IsNullOrWhiteSpace(connection) ||
    new NpgsqlConnectionStringBuilder(connection).Database != "smartparking_recognition")
    throw new InvalidOperationException("ConnectionStrings:Sprint4Recognition must select smartparking_recognition.");

var key = builder.Configuration["Sprint4:RecognitionKey"];
if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
    throw new InvalidOperationException("Sprint4:RecognitionKey requires at least 32 bytes.");

var keyHash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
var aiUrl = builder.Configuration["AiService:BaseUrl"] ?? "http://localhost:8000/";
if (!Uri.TryCreate(aiUrl.TrimEnd('/') + "/", UriKind.Absolute, out var aiUri) || aiUri.Scheme is not ("http" or "https"))
    throw new InvalidOperationException("AiService:BaseUrl must be an HTTP or HTTPS URL.");

var maxBytes = builder.Configuration.GetValue<long?>("AiService:MaxUploadSizeBytes") ?? 10 * 1024 * 1024;
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = maxBytes);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = maxBytes + 64 * 1024);
builder.Services.AddDbContext<AiRecognitionDbContext>(options =>
    options.UseNpgsql(connection, npgsql => npgsql.MigrationsHistoryTable("__AiRecognitionMigrationsHistory")));
builder.Services.AddScoped<IRecognitionEvidenceReader, RecognitionEvidenceReader>();
builder.Services.AddHttpClient<IStaffDetectionService, StaffDetectionService>(client =>
{
    client.BaseAddress = aiUri;
    client.Timeout = TimeSpan.FromSeconds(60);
});

var app = builder.Build();
app.MapGet("/health", () => Results.Ok());

var recognition = app.MapGroup("/internal/recognition");
recognition.AddEndpointFilter(async (context, next) =>
{
    if (!context.HttpContext.Request.Headers.TryGetValue(RecognitionServiceClient.KeyHeader, out var supplied) ||
        supplied.Count != 1 ||
        !CryptographicOperations.FixedTimeEquals(keyHash, SHA256.HashData(Encoding.UTF8.GetBytes(supplied.ToString()))))
        return Results.Unauthorized();

    try { return await next(context); }
    catch (ImageValidationException error) { return Results.BadRequest(new { error.Message }); }
    catch (AiServiceException error) { return Results.Json(new { error.Message }, statusCode: (int)error.StatusCode); }
    catch (HttpRequestException) { return Results.Json(new { message = "OCR engine is unavailable." }, statusCode: 502); }
    catch (OperationCanceledException) when (!context.HttpContext.RequestAborted.IsCancellationRequested)
    { return Results.Json(new { message = "OCR engine timed out." }, statusCode: 504); }
    catch (InvalidDataException) { return Results.BadRequest(new { message = "Invalid or oversized upload." }); }
    catch (Exception error) when (error is NpgsqlException or DbUpdateException)
    { return Results.Json(new { message = "Recognition database is unavailable." }, statusCode: 503); }
});

recognition.MapPost("/detect", async (HttpRequest request, long staffUserId, IStaffDetectionService service, CancellationToken ct) =>
{
    if (staffUserId <= 0 || !request.HasFormContentType)
        return Results.BadRequest(new { message = "A staff ID and an image upload are required." });
    var file = (await request.ReadFormAsync(ct)).Files.GetFile("file");
    if (file is null)
        return Results.BadRequest(new { message = "An image file is required." });

    await using var stream = file.OpenReadStream();
    return Results.Ok(await service.DetectAsync(stream, file.ContentType, file.Length, staffUserId, ct));
});

recognition.MapPost("/{id:long}/confirm", async (
    long id, RecognitionServiceClient.ConfirmRecognition request, IStaffDetectionService service, CancellationToken ct) =>
{
    if (request.StaffUserId <= 0 || string.IsNullOrWhiteSpace(request.PlateNumber) ||
        request.PlateNumber.Length > 32 || request.Note?.Length > 256)
        return Results.BadRequest(new { message = "Invalid recognition confirmation." });

    var result = await service.ConfirmAsync(id, request.PlateNumber, request.Note, request.StaffUserId, ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

recognition.MapGet("/{id:long}/evidence", async (long id, IRecognitionEvidenceReader reader, CancellationToken ct) =>
{
    var result = await reader.FindAsync(id, ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

app.Run();
