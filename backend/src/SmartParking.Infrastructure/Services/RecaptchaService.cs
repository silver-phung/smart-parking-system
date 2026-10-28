using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Common.Options;

namespace SmartParking.Infrastructure.Services;

public class RecaptchaService : IRecaptchaService
{
    private readonly RecaptchaOptions _options;
    private readonly HttpClient _httpClient;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<RecaptchaService> _logger;

    public RecaptchaService(
        IOptions<RecaptchaOptions> options,
        HttpClient httpClient,
        IHostEnvironment environment,
        ILogger<RecaptchaService> logger)
    {
        _options = options.Value;
        _httpClient = httpClient;
        _environment = environment;
        _logger = logger;
    }

    public async Task<bool> VerifyAsync(string? token, string? remoteIp = null, CancellationToken cancellationToken = default)
    {
        // 1. In Production, reCAPTCHA can NEVER be bypassed
        if (_environment.IsProduction() && !_options.Enabled)
        {
            _logger.LogError("CRITICAL SECURITY VIOLATION: reCAPTCHA cannot be disabled in Production. Request rejected.");
            return false;
        }

        // 2. In Non-production environments, allow bypass only if explicitly disabled
        if (!_options.Enabled)
        {
            _logger.LogInformation("reCAPTCHA verification bypassed because it is explicitly disabled in non-production environment.");
            return true;
        }

        // 3. If reCAPTCHA is enabled, SecretKey is mandatory (fail-closed)
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            _logger.LogError("reCAPTCHA is enabled but SecretKey is missing or empty. Request rejected (fail-closed).");
            return false;
        }

        // 4. Client token is mandatory
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("reCAPTCHA verification failed: client token is null or empty.");
            return false;
        }

        try
        {
            var formValues = new Dictionary<string, string>
            {
                { "secret", _options.SecretKey },
                { "response", token }
            };

            if (!string.IsNullOrWhiteSpace(remoteIp))
            {
                formValues.Add("remoteip", remoteIp);
            }

            var content = new FormUrlEncodedContent(formValues);

            var response = await _httpClient.PostAsync(_options.VerifyUrl, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("reCAPTCHA verification HTTP call returned status {StatusCode}", response.StatusCode);
                return false;
            }

            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<GoogleRecaptchaResponse>(responseString);

            if (result == null || !result.Success)
            {
                _logger.LogWarning("reCAPTCHA verification returned unsuccessful result from provider.");
                return false;
            }

            if (result.Score.HasValue && result.Score.Value < _options.MinScore)
            {
                _logger.LogWarning("reCAPTCHA verification score {Score} is below threshold {MinScore}.", result.Score, _options.MinScore);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "reCAPTCHA verification exception during provider communication. Failing closed.");
            return false;
        }
    }

    private class GoogleRecaptchaResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("score")]
        public double? Score { get; set; }

        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("challenge_ts")]
        public string? ChallengeTs { get; set; }

        [JsonPropertyName("hostname")]
        public string? Hostname { get; set; }
    }
}
