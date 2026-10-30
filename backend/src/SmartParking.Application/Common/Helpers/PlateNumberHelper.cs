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

    /// <summary>
    /// Chuẩn hóa biển số để đối chiếu (loại bỏ ký tự phân cách như dấu cách, gạch nối, chấm và in hoa).
    /// </summary>
    public static string NormalizeForComparison(string? plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber))
        {
            return string.Empty;
        }

        var sb = new System.Text.StringBuilder(plateNumber.Length);
        foreach (char c in plateNumber)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(char.ToUpperInvariant(c));
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Kiểm tra hai chuỗi biển số có trùng khớp danh tính hay không.
    /// </summary>
    public static bool IsMatch(string? plate1, string? plate2)
    {
        var norm1 = NormalizeForComparison(plate1);
        var norm2 = NormalizeForComparison(plate2);
        return !string.IsNullOrEmpty(norm1) && norm1 == norm2;
    }
}
