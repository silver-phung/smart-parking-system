namespace SmartParking.Application.Common.Interfaces;

/// <summary>
/// Interface gọi Gemini API để phân tích ảnh xe qua cổng vào.
///
/// Luồng Sprint 3:
///   Camera cổng → upload ảnh lên Object Storage → nhận URL
///   → IGeminiVehicleAnalyzer.AnalyzeAsync(imageUrl)
///   → GeminiVehicleAnalysisResult { CarPlate, FuelType, CarColor, CarModel }
///   → Staff cổng xác nhận → POST /api/events { eventType: VehicleEntered, ... }
/// </summary>
public interface IGeminiVehicleAnalyzer
{
    /// <summary>
    /// Phân tích ảnh xe để trích xuất thông tin biển số, loại nhiên liệu, màu sắc và hãng xe.
    /// </summary>
    /// <param name="imageUrl">URL công khai của ảnh xe (JPG/PNG, tối đa 10 MB).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Kết quả phân tích từ Gemini. Các trường có thể null nếu Gemini không nhận ra.</returns>
    Task<GeminiVehicleAnalysisResult> AnalyzeAsync(string imageUrl, CancellationToken ct = default);
}

/// <summary>
/// Kết quả phân tích ảnh xe từ Gemini 2.5 Pro.
/// </summary>
public sealed class GeminiVehicleAnalysisResult
{
    /// <summary>
    /// Biển số xe nhận diện từ ảnh, ví dụ: "51F-12345".
    /// Null nếu Gemini không thể đọc biển số (ảnh mờ, che khuất, v.v.).
    /// </summary>
    public string? CarPlate { get; init; }

    /// <summary>
    /// Loại nhiên liệu: "gasoline" | "electric" | "unknown".
    /// Gemini nhận diện dựa vào hình dạng xe, logo hãng, cổng sạc, ống xả.
    /// </summary>
    public string FuelType { get; init; } = "unknown";

    /// <summary>
    /// Màu chủ đạo của xe, ví dụ: "white", "black", "silver", "red".
    /// </summary>
    public string? CarColor { get; init; }

    /// <summary>
    /// Hãng và model xe, ví dụ: "Toyota Vios", "VinFast VF8", "Honda City".
    /// </summary>
    public string? CarModel { get; init; }

    /// <summary>
    /// Mức độ tự tin của Gemini (0.0 – 1.0). Dưới 0.5 cần Staff xác nhận lại.
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Thông báo lỗi nếu Gemini không thể xử lý ảnh.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>True nếu phân tích thành công và dữ liệu đáng tin cậy.</summary>
    public bool IsSuccess => ErrorMessage is null;
}
