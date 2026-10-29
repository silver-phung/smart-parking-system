namespace SmartParking.Application.Common.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "SmartParking";
    public string Audience { get; set; } = "SmartParkingClients";
    public string SecretKey { get; set; } = string.Empty;
    public int AccessTokenLifetimeMinutes { get; set; } = 15;
    public int RefreshTokenLifetimeDays { get; set; } = 7;

    // Aliases to ensure seamless configuration binding
    public int AccessTokenExpirationMinutes
    {
        get => AccessTokenLifetimeMinutes;
        set => AccessTokenLifetimeMinutes = value;
    }

    public int RefreshTokenExpirationDays
    {
        get => RefreshTokenLifetimeDays;
        set => RefreshTokenLifetimeDays = value;
    }
}

public class SecurityOptions
{
    public const string SectionName = "Security";

    public int MaxFailedLoginAttempts { get; set; } = 5;
    public int LockoutDurationMinutes { get; set; } = 15;
    public string OtpSecretKey { get; set; } = "SmartParkingSecureOtpHmacSecretKey2026!#";
    public int OtpLifetimeMinutes { get; set; } = 15;
    public int ResendCooldownSeconds { get; set; } = 60;
    public int MaxOtpAttempts { get; set; } = 5;
}

public class AdminBootstrapOptions
{
    public const string SectionName = "AdminBootstrap";

    public bool Enabled { get; set; } = false;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = "System Administrator";
    public string? PhoneNumber { get; set; }
}

public class EmailOptions
{
    public const string SectionName = "Email";

    public string Provider { get; set; } = "Mock"; // Smtp or Mock
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = "noreply@smartparking.vn";
    public string SenderName { get; set; } = "Smart Parking System";
    public bool EnableSsl { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 10;
}

public class RecaptchaOptions
{
    public const string SectionName = "Recaptcha";

    public bool Enabled { get; set; } = false;
    public string SecretKey { get; set; } = string.Empty;
    public string SiteKey { get; set; } = string.Empty;
    public string VerifyUrl { get; set; } = "https://www.google.com/recaptcha/api/siteverify";
    public double MinScore { get; set; } = 0.5;
}


