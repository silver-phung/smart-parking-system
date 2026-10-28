using Microsoft.AspNetCore.Mvc;
using SmartParking.Application.Common.Interfaces;

namespace SmartParking.Api.Controllers;

/// <summary>
/// API phân tích ảnh xe qua Gemini 2.5 Pro.
///
/// Luồng sử dụng (Sprint 3):
///   1. Camera cổng chụp ảnh xe
///   2. Client upload ảnh lên Object Storage → nhận URL
///   3. Gọi POST /api/gemini/analyze?imageUrl=... để lấy thông tin xe
///   4. Staff xem kết quả, xác nhận rồi gửi POST /api/events { VehicleEntered }
/// </summary>
[ApiController]
[Route("api/gemini")]
[Produces("application/json")]
public class GeminiController : ControllerBase
{
    private readonly IGeminiVehicleAnalyzer          _analyzer;
    private readonly ILogger<GeminiController> _logger;

    public GeminiController(
        IGeminiVehicleAnalyzer          analyzer,
        ILogger<GeminiController> logger)
    {
        _analyzer = analyzer;
        _logger   = logger;
    }

    /// <summary>
    /// Phân tích ảnh xe để trích xuất biển số, loại nhiên liệu, màu sắc và hãng xe.
    /// Gemini 2.5 Pro sẽ nhận diện và trả về kết quả JSON.
    /// </summary>
    /// <param name="imageUrl">URL công khai của ảnh xe (JPG/PNG, tối đa 10 MB).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Kết quả phân tích từ Gemini.</returns>
    [HttpPost("analyze")]
    [ProducesResponseType(typeof(GeminiAnalyzeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeminiAnalyzeResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AnalyzeVehicle(
        [FromQuery] string imageUrl,
        CancellationToken  ct)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return BadRequest(new { message = "imageUrl là bắt buộc." });

        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out _))
            return BadRequest(new { message = "imageUrl không hợp lệ." });

        _logger.LogInformation("[GeminiController] Yêu cầu phân tích ảnh: {Url}", imageUrl);

        var result = await _analyzer.AnalyzeAsync(imageUrl, ct);

        var response = new GeminiAnalyzeResponse
        {
            ImageUrl   = imageUrl,
            CarPlate   = result.CarPlate,
            FuelType   = result.FuelType,
            CarColor   = result.CarColor,
            CarModel   = result.CarModel,
            Confidence = result.Confidence,
            IsSuccess  = result.IsSuccess,
            ErrorMessage = result.ErrorMessage
        };

        return result.IsSuccess
            ? Ok(response)
            : UnprocessableEntity(response);
    }
}

/// <summary>Kết quả phân tích ảnh xe trả về cho client.</summary>
public sealed class GeminiAnalyzeResponse
{
    public string ImageUrl { get; init; } = string.Empty;

    /// <summary>Biển số xe nhận diện. Null nếu Gemini không đọc được.</summary>
    public string? CarPlate { get; init; }

    /// <summary>"gasoline" | "electric" | "unknown"</summary>
    public string FuelType { get; init; } = "unknown";

    /// <summary>Màu xe, ví dụ "white", "black", "silver".</summary>
    public string? CarColor { get; init; }

    /// <summary>Hãng và model, ví dụ "Toyota Vios", "VinFast VF8".</summary>
    public string? CarModel { get; init; }

    /// <summary>Mức độ tự tin 0.0 – 1.0. Dưới 0.5 Staff cần xác nhận lại.</summary>
    public double Confidence { get; init; }

    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
}
