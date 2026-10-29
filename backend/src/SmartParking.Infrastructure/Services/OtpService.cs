using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Common.Options;

namespace SmartParking.Infrastructure.Services;

public class OtpService : IOtpService
{
    private readonly SecurityOptions _options;
    private readonly byte[] _secretKeyBytes;

    public OtpService(IOptions<SecurityOptions> options)
    {
        _options = options.Value;
        var key = string.IsNullOrWhiteSpace(_options.OtpSecretKey) ? "SmartParkingSecureOtpHmacSecretKey2026!#" : _options.OtpSecretKey;
        if (Encoding.UTF8.GetByteCount(key) < 32)
        {
            key = key.PadRight(32, '!');
        }
        _secretKeyBytes = Encoding.UTF8.GetBytes(key);
    }

    public string GenerateOtpCode()
    {
        // Generates cryptographically secure 6-digit code between 100000 and 999999
        var codeInt = RandomNumberGenerator.GetInt32(100000, 1000000);
        return codeInt.ToString("D6");
    }

    public string ComputeOtpHash(long userId, string tokenType, string otpCode)
    {
        var payload = $"{userId}:{tokenType}:{otpCode}";
        using var hmac = new HMACSHA256(_secretKeyBytes);
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public bool VerifyOtpHash(long userId, string tokenType, string otpCode, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(otpCode) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        var computedHash = ComputeOtpHash(userId, tokenType, otpCode);
        var computedBytes = Encoding.UTF8.GetBytes(computedHash);
        var storedBytes = Encoding.UTF8.GetBytes(storedHash.ToLowerInvariant());

        return CryptographicOperations.FixedTimeEquals(computedBytes, storedBytes);
    }
}
