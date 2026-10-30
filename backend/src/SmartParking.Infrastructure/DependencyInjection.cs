using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Common.Options;
using SmartParking.Infrastructure.Data;
using SmartParking.Infrastructure.Repositories;
using SmartParking.Infrastructure.Services;

namespace SmartParking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Database Context
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddDbContext<SmartParkingDbContext>(options =>
                options.UseNpgsql(connectionString, b => b.MigrationsAssembly(typeof(SmartParkingDbContext).Assembly.FullName)));
        }
        else
        {
            services.AddDbContext<SmartParkingDbContext>(options =>
                options.UseNpgsql(b => b.MigrationsAssembly(typeof(SmartParkingDbContext).Assembly.FullName)));
        }

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<SmartParkingDbContext>());

        // 2. Strongly Typed Options
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<SecurityOptions>(configuration.GetSection(SecurityOptions.SectionName));
        services.Configure<AdminBootstrapOptions>(configuration.GetSection(AdminBootstrapOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<RecaptchaOptions>(configuration.GetSection(RecaptchaOptions.SectionName));
        services.Configure<CloudinaryOptions>(options =>
        {
            configuration.GetSection(CloudinaryOptions.SectionName).Bind(options);
            options.Url = configuration["CLOUDINARY_URL"] ?? options.Url;
            options.CloudName = configuration["CLOUDINARY_CLOUD_NAME"] ?? options.CloudName;
            options.ApiKey = configuration["CLOUDINARY_API_KEY"] ?? options.ApiKey;
            options.ApiSecret = configuration["CLOUDINARY_API_SECRET"] ?? options.ApiSecret;
        });

        // 3. Infrastructure & Auth Services
        services.AddScoped<IPasswordHasherService, PasswordHasherService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddHttpClient<IRecaptchaService, RecaptchaService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAdminBootstrapService, AdminBootstrapService>();
        services.AddScoped<IParkingManagementService, ParkingManagementService>();
        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<IParkingSessionService, ParkingSessionService>();
        services.AddScoped<IParkingDispatchService, ParkingDispatchService>();
        services.AddScoped<IParkingAuthorizationService, ParkingAuthorizationService>();
        services.AddScoped<IStaffLotAssignmentService, StaffLotAssignmentService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IImageService, ImageService>();
        var aiConnectionString = configuration.GetConnectionString("AiRecognitionConnection");
        if (!string.IsNullOrWhiteSpace(aiConnectionString))
        {
            services.AddDbContext<AiRecognitionDbContext>(options =>
                options.UseNpgsql(aiConnectionString, b => b
                    .MigrationsAssembly(typeof(AiRecognitionDbContext).Assembly)
                    .MigrationsHistoryTable("__AiRecognitionMigrationsHistory")));
        }
        else
        {
            services.AddDbContext<AiRecognitionDbContext>(options =>
                options.UseNpgsql(b => b
                    .MigrationsAssembly(typeof(AiRecognitionDbContext).Assembly)
                    .MigrationsHistoryTable("__AiRecognitionMigrationsHistory")));
        }
        services.AddHttpClient<IStaffDetectionService, StaffDetectionService>((provider, client) =>
        {
            var baseUrl = provider.GetRequiredService<IConfiguration>()["AiService:BaseUrl"];
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
                throw new InvalidOperationException("AiService:BaseUrl must be a valid absolute URL.");
            client.BaseAddress = uri.AbsoluteUri.EndsWith('/') ? uri : new Uri(uri, "/");
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        var relativePath = configuration["ParkingLots:DataFile"] ?? "Data/parking-lots.json";
        var fullPath = Path.Combine(AppContext.BaseDirectory, relativePath);
        if (!File.Exists(fullPath))
        {
            fullPath = Path.GetFullPath(relativePath);
        }
        services.AddScoped<IParkingLotRepository>(_ => new ParkingLotRepository(fullPath));

        // Sprint 3 – Thức: Event Ingestion Pipeline (in-memory, sẵn sàng chuyển sang EF khi DB hoàn tất)
        services.AddSingleton<IParkingEventLogRepository, InMemoryParkingEventLogRepository>();
        services.AddSingleton<IParkingSessionRepository,  InMemoryParkingSessionRepository>();

        // Sprint 3 Day 3 – Thức: Vehicle compatibility stub (TODO: replace with EF when Phụng's branch merged)
        services.AddSingleton<IVehicleCompatibilityChecker, AlwaysCompatibleVehicleChecker>();

        // Sprint 3 Day 6 – Thức: Gemini AI Integration
        services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));
        services.AddHttpClient<GeminiVehicleAnalyzerService>();

        var geminiApiKey = configuration["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(geminiApiKey))
        {
            services.AddSingleton<IGeminiVehicleAnalyzer, NoOpGeminiVehicleAnalyzer>();
        }
        else
        {
            services.AddScoped<IGeminiVehicleAnalyzer, GeminiVehicleAnalyzerService>();
        }

        // Sprint 3 Day 8 – Thức: Bridge to Phung's DB/Service
        services.AddScoped<IParkingEventBridge, NoOpParkingEventBridge>();

        return services;
    }
}
