using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Common.Options;
using SmartParking.Infrastructure.Data;
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
        services.Configure<RecaptchaOptions>(configuration.GetSection(RecaptchaOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<AdminBootstrapOptions>(configuration.GetSection(AdminBootstrapOptions.SectionName));
        services.Configure<CloudinaryOptions>(options =>
        {
            configuration.GetSection(CloudinaryOptions.SectionName).Bind(options);
            options.Url = configuration["CLOUDINARY_URL"] ?? options.Url;
            options.CloudName = configuration["CLOUDINARY_CLOUD_NAME"] ?? options.CloudName;
            options.ApiKey = configuration["CLOUDINARY_API_KEY"] ?? options.ApiKey;
            options.ApiSecret = configuration["CLOUDINARY_API_SECRET"] ?? options.ApiSecret;
        });

        // 3. Infrastructure & Auth Services
        services.AddHttpClient();
        services.AddScoped<IPasswordHasherService, PasswordHasherService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IRecaptchaService, RecaptchaService>();
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

        return services;
    }
}
