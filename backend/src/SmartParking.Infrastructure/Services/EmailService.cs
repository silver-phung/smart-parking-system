using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Common.Options;

namespace SmartParking.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IOptions<EmailOptions> options,
        IHostEnvironment environment,
        ILogger<EmailService> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<bool> SendVerificationOtpAsync(string toEmail, string otpCode, CancellationToken cancellationToken = default)
    {
        // 1. In Production, Mock provider is STRICTLY FORBIDDEN (Fail-Closed)
        if (_environment.IsProduction() && string.Equals(_options.Provider, "Mock", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError("CRITICAL SECURITY VIOLATION: Email:Provider cannot be set to 'Mock' in Production environment. Email sending rejected.");
            return false;
        }

        // 2. In Non-production (Development/Testing), allow Mock provider simulation
        if (string.Equals(_options.Provider, "Mock", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("[DEV EMAIL SIMULATION] Verification OTP for {Recipient}: {OtpCode} (Expires in 15 minutes).", toEmail, otpCode);
            return true;
        }

        // 3. Validate required SMTP settings
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            _logger.LogError("Email sending failed: SMTP Host is not configured.");
            return false;
        }

        // 4. In Production, anonymous relay without credentials is not permitted
        if (_environment.IsProduction() && (string.IsNullOrWhiteSpace(_options.UserName) || string.IsNullOrWhiteSpace(_options.Password)))
        {
            _logger.LogError("CRITICAL SECURITY VIOLATION: In Production, SMTP UserName and Password must be configured. Anonymous relay is rejected.");
            return false;
        }

        // 5. In Development, if using Gmail SMTP without credentials, fail fast with actionable guidance
        if (_options.Host.Contains("gmail.com", StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(_options.UserName) || string.IsNullOrWhiteSpace(_options.Password)))
        {
            _logger.LogWarning("Email sending skipped: Gmail SMTP requires authentication. Please configure credentials via User Secrets (e.g. dotnet user-secrets set \"Email:UserName\" \"...\" and \"Email:Password\" \"...\") or switch Email:Provider to 'Mock' for local simulation.");
            return false;
        }

        // Resolve effective sender email (if SenderEmail is default placeholder 'noreply@smartparking.vn' or empty, use UserName if it's an email)
        var senderEmail = !string.IsNullOrWhiteSpace(_options.SenderEmail) && !string.Equals(_options.SenderEmail, "noreply@smartparking.vn", StringComparison.OrdinalIgnoreCase)
            ? _options.SenderEmail
            : (!string.IsNullOrWhiteSpace(_options.UserName) && _options.UserName.Contains('@') ? _options.UserName : _options.SenderEmail);

        if (string.IsNullOrWhiteSpace(senderEmail))
        {
            _logger.LogError("Email sending failed: SenderEmail is not configured.");
            return false;
        }

        try
        {
            var timeoutMs = _options.TimeoutSeconds > 0 ? _options.TimeoutSeconds * 1000 : 10000;
            using var client = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Timeout = timeoutMs
            };

            if (!string.IsNullOrWhiteSpace(_options.UserName) && !string.IsNullOrWhiteSpace(_options.Password))
            {
                var sanitizedPassword = _options.Password.Replace(" ", "").Trim();
                client.Credentials = new NetworkCredential(_options.UserName.Trim(), sanitizedPassword);
            }

            var subject = "Mã xác minh tài khoản Smart Parking";
            var plainTextBody = $@"Xin chào,

Cảm ơn bạn đã đăng ký tài khoản tại Hệ thống Smart Parking.
Mã OTP xác minh email của bạn là: {otpCode}

Mã này có hiệu lực trong vòng 15 phút. Vui lòng không chia sẻ mã này cho bất kỳ ai.

Trân trọng,
Đội ngũ Smart Parking System";

            var htmlBody = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"" />
    <title>Mã xác minh Smart Parking</title>
</head>
<body style=""font-family: Arial, sans-serif; background-color: #f4f6f9; margin: 0; padding: 24px; color: #333333;"">
    <div style=""max-width: 520px; margin: 0 auto; background: #ffffff; border-radius: 8px; border: 1px solid #e2e8f0; padding: 32px; box-shadow: 0 2px 4px rgba(0,0,0,0.05);"">
        <h2 style=""color: #1e3a8a; margin-top: 0; font-size: 22px;"">Smart Parking System</h2>
        <p style=""font-size: 15px; line-height: 1.5;"">Xin chào,</p>
        <p style=""font-size: 15px; line-height: 1.5;"">Cảm ơn bạn đã đăng ký tài khoản tại <strong>Hệ thống Smart Parking</strong>. Dưới đây là mã xác minh OTP của bạn:</p>
        <div style=""margin: 24px 0; text-align: center;"">
            <span style=""display: inline-block; font-size: 32px; font-weight: bold; letter-spacing: 6px; color: #1e40af; background: #eff6ff; padding: 12px 24px; border-radius: 6px; border: 1px dashed #3b82f6;"">{otpCode}</span>
        </div>
        <p style=""font-size: 14px; color: #64748b; line-height: 1.5;"">Mã OTP này có hiệu lực trong vòng <strong>15 phút</strong>. Vì lý do an toàn, vui lòng tuyệt đối không chia sẻ mã này cho bất kỳ ai.</p>
        <hr style=""border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;"" />
        <p style=""font-size: 12px; color: #94a3b8; margin-bottom: 0;"">Trân trọng,<br />Đội ngũ Smart Parking System</p>
    </div>
</body>
</html>";

            using var message = new MailMessage
            {
                From = new MailAddress(senderEmail, _options.SenderName),
                Subject = subject,
                Body = plainTextBody,
                IsBodyHtml = false
            };
            message.To.Add(toEmail);

            var htmlView = AlternateView.CreateAlternateViewFromString(htmlBody, System.Text.Encoding.UTF8, "text/html");
            message.AlternateViews.Add(htmlView);

            await client.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("Verification email sent successfully to {Recipient}.", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send verification email via SMTP to {Recipient}. External email service error.", toEmail);
            return false;
        }
    }
}
