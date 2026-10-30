using Microsoft.Extensions.Logging;
using SmartParking.Application.Common.Interfaces;

namespace SmartParking.Infrastructure.Services;

/// <summary>
/// No-op implementation dùng khi chưa có Gemini ApiKey.
/// Trả về dữ liệu rỗng và ghi log warning để nhắc cấu hình.
/// Dùng trong development / unit test khi không muốn gọi API thật.
/// </summary>
public class NoOpGeminiVehicleAnalyzer : IGeminiVehicleAnalyzer
{
    private readonly ILogger<NoOpGeminiVehicleAnalyzer> _logger;

    public NoOpGeminiVehicleAnalyzer(ILogger<NoOpGeminiVehicleAnalyzer> logger)
    {
        _logger = logger;
    }

    public Task<GeminiVehicleAnalysisResult> AnalyzeAsync(
        string imageUrl, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "[Gemini NoOp] Đang dùng stub – chưa cấu hình ApiKey. " +
            "Thêm 'Gemini:ApiKey' vào User Secrets để kích hoạt phân tích thật. " +
            "ImageUrl={Url}", imageUrl);

        return Task.FromResult(new GeminiVehicleAnalysisResult
        {
            FuelType     = "unknown",
            Confidence   = 0,
            ErrorMessage = "Gemini chưa được cấu hình (NoOp mode)."
        });
    }
}
