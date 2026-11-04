using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
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
        var useSprint4 = configuration.GetValue<bool>("Sprint4:Enabled");
        var connectionString = configuration.GetConnectionString(useSprint4 ? "Sprint4Core" : "DefaultConnection");
        if (useSprint4 && (string.IsNullOrWhiteSpace(connectionString) ||
            new NpgsqlConnectionStringBuilder(connectionString).Database != "smartparking_core_sprint4"))
            throw new InvalidOperationException("ConnectionStrings:Sprint4Core must select smartparking_core_sprint4.");
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
        services.AddScoped<ParkingOperationsService>();
        services.AddScoped<IParkingDispatchService, ParkingDispatchService>();
        services.AddScoped<IParkingAuthorizationService, ParkingAuthorizationService>();
        services.AddScoped<IStaffLotAssignmentService, StaffLotAssignmentService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IImageService, ImageService>();
        if (useSprint4)
        {
            var url = configuration["Sprint4:RecognitionUrl"];
            var key = configuration["Sprint4:RecognitionKey"];
            if (string.IsNullOrWhiteSpace(url) ||
                !Uri.TryCreate(url.TrimEnd('/') + "/", UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)) ||
                !string.IsNullOrEmpty(uri.UserInfo) || uri.AbsolutePath != "/" || uri.Query != "" || uri.Fragment != "")
                throw new InvalidOperationException("Sprint4:RecognitionUrl must be a service origin using HTTPS or loopback HTTP.");
            if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
                throw new InvalidOperationException("Sprint4:RecognitionKey requires at least 32 bytes.");

            services.AddHttpClient<RecognitionServiceClient>(client =>
            {
                client.BaseAddress = uri;
                client.Timeout = TimeSpan.FromSeconds(65);
                client.DefaultRequestHeaders.Add(RecognitionServiceClient.KeyHeader, key);
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
            services.AddTransient<IRecognitionEvidenceReader>(provider => provider.GetRequiredService<RecognitionServiceClient>());
            services.AddTransient<IStaffDetectionService>(provider => provider.GetRequiredService<RecognitionServiceClient>());
        }
        else
        {
            var aiConnectionString = configuration.GetConnectionString("AiRecognitionConnection");
            services.AddDbContext<AiRecognitionDbContext>(options =>
                options.UseNpgsql(aiConnectionString, b => b
                    .MigrationsAssembly(typeof(AiRecognitionDbContext).Assembly)
                    .MigrationsHistoryTable("__AiRecognitionMigrationsHistory")));
            services.AddScoped<IRecognitionEvidenceReader, RecognitionEvidenceReader>();
            services.AddHttpClient<IStaffDetectionService, StaffDetectionService>((provider, client) =>
            {
                var baseUrl = provider.GetRequiredService<IConfiguration>()["AiService:BaseUrl"];
                if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
                    throw new InvalidOperationException("AiService:BaseUrl must be a valid absolute URL.");
                client.BaseAddress = uri.AbsoluteUri.EndsWith('/') ? uri : new Uri(uri, "/");
                client.Timeout = TimeSpan.FromSeconds(60);
            });
        }
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
