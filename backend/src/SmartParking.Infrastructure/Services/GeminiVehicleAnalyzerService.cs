using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartParking.Application.Common.Interfaces;

namespace SmartParking.Infrastructure.Services;

/// <summary>
/// Gọi Gemini 2.5 Pro API (google/gemini-2.5-pro:generateContent) để phân tích ảnh xe.
///
/// Cấu hình cần thiết trong appsettings.json / User Secrets:
/// <code>
/// "Gemini": {
///   "ApiKey": "AIza...",
///   "Model": "gemini-2.5-pro",
///   "MaxRetries": 3
/// }
/// </code>
///
/// Nếu chưa có API key, dùng <see cref="NoOpGeminiVehicleAnalyzer"/> để mock.
/// </summary>
public class GeminiVehicleAnalyzerService : IGeminiVehicleAnalyzer
{
    private readonly HttpClient                      _http;
    private readonly GeminiOptions                   _options;
    private readonly ILogger<GeminiVehicleAnalyzerService> _logger;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull
    };

    public GeminiVehicleAnalyzerService(
        HttpClient                           http,
        IOptions<GeminiOptions>              options,
        ILogger<GeminiVehicleAnalyzerService> logger)
    {
        _http    = http;
        _options = options.Value;
        _logger  = logger;
    }

    public async Task<GeminiVehicleAnalysisResult> AnalyzeAsync(
        string imageUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("[Gemini] ApiKey chưa được cấu hình – trả về kết quả rỗng.");
            return new GeminiVehicleAnalysisResult
            {
                ErrorMessage = "Gemini ApiKey chưa cấu hình. Vui lòng thêm 'Gemini:ApiKey' vào User Secrets.",
                Confidence   = 0
            };
        }

        // ── 1. Build Gemini request ───────────────────────────────────────────
        var prompt = BuildPrompt();
        var requestBody = new GeminiRequest
        {
            Contents =
            [
                new GeminiContent
                {
                    Parts =
                    [
                        // Image inline via URL (Gemini 2.5 hỗ trợ URL trực tiếp)
                        new GeminiPart { FileData = new GeminiFileData { FileUri = imageUrl, MimeType = "image/jpeg" } },
                        new GeminiPart { Text     = prompt }
                    ]
                }
            ],
            GenerationConfig = new GeminiGenerationConfig
            {
                Temperature      = 0.1,
                MaxOutputTokens  = 256,
                ResponseMimeType = "application/json"
            }
        };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_options.Model}:generateContent?key={_options.ApiKey}";

        // ── 2. Call API với retry đơn giản ───────────────────────────────────
        for (var attempt = 1; attempt <= _options.MaxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("[Gemini] Phân tích ảnh: {Url} (attempt {Attempt}/{Max})",
                    imageUrl, attempt, _options.MaxRetries);

                var response = await _http.PostAsJsonAsync(url, requestBody, _json, ct);
                response.EnsureSuccessStatusCode();

                var geminiResponse = await response.Content.ReadFromJsonAsync<GeminiResponse>(_json, ct);
                return ParseResponse(geminiResponse, imageUrl);
            }
            catch (Exception ex) when (attempt < _options.MaxRetries)
            {
                _logger.LogWarning(ex, "[Gemini] Attempt {Attempt} thất bại, retry sau 2s...", attempt);
                await Task.Delay(2000, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Gemini] Gọi API thất bại sau {Max} lần thử.", _options.MaxRetries);
                return new GeminiVehicleAnalysisResult
                {
                    ErrorMessage = $"Gemini API lỗi: {ex.Message}",
                    Confidence   = 0
                };
            }
        }

        // Không bao giờ đến đây nhưng compiler cần return
        return new GeminiVehicleAnalysisResult { ErrorMessage = "Không thể gọi Gemini API.", Confidence = 0 };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    private static string BuildPrompt() =>
        """
        Phân tích ảnh xe hơi này và trả về JSON với đúng cấu trúc sau (không có text thêm):
        {
          "carPlate":   "<biển số xe, VD: 51F-12345, hoặc null nếu không đọc được>",
          "fuelType":   "<gasoline|electric|unknown>",
          "carColor":   "<màu chủ đạo: white|black|silver|red|blue|... hoặc null>",
          "carModel":   "<hãng + model: Toyota Vios|VinFast VF8|Honda City|... hoặc null>",
          "confidence": <0.0 đến 1.0 – mức độ chắc chắn về biển số>
        }
        Quy tắc:
        - biển số Việt Nam thường có dạng: XX[A-Z]-XXXXX (ví dụ 51F-12345, 30A-99999, 72H-00001)
        - fuelType: dùng cổng sạc/ống xả/logo để phân biệt xăng/điện
        - Nếu không chắc, dùng "unknown" cho fuelType và null cho các trường còn lại
        """;

    private GeminiVehicleAnalysisResult ParseResponse(GeminiResponse? response, string imageUrl)
    {
        try
        {
            var text = response?.Candidates?[0].Content?.Parts?[0].Text;
            if (string.IsNullOrWhiteSpace(text))
                return new GeminiVehicleAnalysisResult { ErrorMessage = "Gemini trả về kết quả rỗng.", Confidence = 0 };

            // Strip markdown code fence nếu có
            text = text.Trim();
            if (text.StartsWith("```"))
            {
                var start = text.IndexOf('{');
                var end = text.LastIndexOf('}');
                if (start >= 0 && end >= start)
                {
                    text = text.Substring(start, end - start + 1);
                }
            }

            var parsed = JsonSerializer.Deserialize<GeminiParsedResult>(text, _json);
            if (parsed is null)
                return new GeminiVehicleAnalysisResult { ErrorMessage = "Không parse được JSON từ Gemini.", Confidence = 0 };

            _logger.LogInformation("[Gemini] Kết quả: plate={Plate} fuel={Fuel} color={Color} model={Model} conf={Conf}",
                parsed.CarPlate, parsed.FuelType, parsed.CarColor, parsed.CarModel, parsed.Confidence);

            return new GeminiVehicleAnalysisResult
            {
                CarPlate   = parsed.CarPlate,
                FuelType   = parsed.FuelType ?? "unknown",
                CarColor   = parsed.CarColor,
                CarModel   = parsed.CarModel,
                Confidence = parsed.Confidence
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Gemini] Lỗi parse response cho ảnh {Url}", imageUrl);
            return new GeminiVehicleAnalysisResult { ErrorMessage = $"Parse lỗi: {ex.Message}", Confidence = 0 };
        }
    }

    // ─── Internal DTOs chỉ dùng cho Gemini API request/response ────────────

    private sealed record GeminiRequest
    {
        [JsonPropertyName("contents")]
        public required GeminiContent[] Contents { get; init; }

        [JsonPropertyName("generationConfig")]
        public GeminiGenerationConfig? GenerationConfig { get; init; }
    }

    private sealed record GeminiContent
    {
        [JsonPropertyName("parts")]
        public required GeminiPart[] Parts { get; init; }
    }

    private sealed record GeminiPart
    {
        [JsonPropertyName("text")]
        public string? Text { get; init; }

        [JsonPropertyName("fileData")]
        public GeminiFileData? FileData { get; init; }
    }

    private sealed record GeminiFileData
    {
        [JsonPropertyName("fileUri")]
        public required string FileUri { get; init; }

        [JsonPropertyName("mimeType")]
        public string MimeType { get; init; } = "image/jpeg";
    }

    private sealed record GeminiGenerationConfig
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; init; } = 0.1;

        [JsonPropertyName("maxOutputTokens")]
        public int MaxOutputTokens { get; init; } = 256;

        [JsonPropertyName("responseMimeType")]
        public string ResponseMimeType { get; init; } = "application/json";
    }

    private sealed record GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public GeminiCandidate[]? Candidates { get; init; }
    }

    private sealed record GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; init; }
    }

    private sealed record GeminiParsedResult
    {
        [JsonPropertyName("carPlate")]   public string? CarPlate   { get; init; }
        [JsonPropertyName("fuelType")]   public string? FuelType   { get; init; }
        [JsonPropertyName("carColor")]   public string? CarColor   { get; init; }
        [JsonPropertyName("carModel")]   public string? CarModel   { get; init; }
        [JsonPropertyName("confidence")] public double  Confidence { get; init; }
    }
}

/// <summary>
/// Cấu hình Gemini API – đọc từ section "Gemini" trong appsettings / User Secrets.
/// </summary>
public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// <summary>API Key từ Google AI Studio (https://aistudio.google.com/).</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>Model ID, mặc định "gemini-2.5-pro".</summary>
    public string Model { get; init; } = "gemini-2.5-pro";

    /// <summary>Số lần retry khi API lỗi tạm thời.</summary>
    public int MaxRetries { get; init; } = 3;
}
