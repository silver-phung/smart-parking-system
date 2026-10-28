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
