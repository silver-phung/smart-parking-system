using System.Text.RegularExpressions;

namespace SmartParking.Application.Common.Helpers;

public static class PlateNumberHelper
{
    private static readonly Regex MultipleSpacesRegex = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Chuẩn hóa biển số xe: loại bỏ khoảng trắng dư thừa và chuyển sang chữ hoa (in hoa).
    /// </summary>
    public static string Normalize(string? plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber))
        {
            return string.Empty;
        }

        var trimmed = plateNumber.Trim().ToUpperInvariant();
        return MultipleSpacesRegex.Replace(trimmed, " ");
    }

    /// <summary>
    /// Kiểm tra định dạng cơ bản của biển số (độ dài 3-20 ký tự).
    /// </summary>
    public static bool IsValidPlateFormat(string? plateNumber)
    {
        var normalized = Normalize(plateNumber);
        return normalized.Length >= 3 && normalized.Length <= 20;
    }
}
