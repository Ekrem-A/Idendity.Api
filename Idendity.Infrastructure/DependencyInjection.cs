using System.Text;
using Idendity.Application.Interfaces;
using Idendity.Application.Settings;
using Idendity.Domain.Constants;
using Idendity.Infrastructure.Identity;
using Idendity.Infrastructure.Persistence;
using Idendity.Infrastructure.Repositories;
using Idendity.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Idendity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure DbContext
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        // Configure Identity
        services.AddIdentity<ApplicationUserIdentity, ApplicationRoleIdentity>(options =>
        {
            // Password settings
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;
            options.Password.RequiredUniqueChars = 4;

            // Lockout settings
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            // User settings
            options.User.RequireUniqueEmail = true;
            options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

            // SignIn settings
            options.SignIn.RequireConfirmedEmail = false; // Set to true in production
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        // Configure JWT Settings
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException("JWT settings not configured");
        
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        // Configure JWT Authentication
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.SaveToken = true;
            options.RequireHttpsMetadata = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(jwtSettings.ClockSkewSeconds),
                RequireExpirationTime = true
            };

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                    {
                        context.Response.Headers["Token-Expired"] = "true";
                    }
                    return Task.CompletedTask;
                }
            };
        });

        // Configure Authorization policies
        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.RequireAdmin, policy => policy.RequireRole(Roles.Admin))
            .AddPolicy(Policies.RequireSeller, policy => policy.RequireRole(Roles.Admin, Roles.Seller))
            .AddPolicy(Policies.RequireSupport, policy => policy.RequireRole(Roles.Admin, Roles.Support));

        // Register services
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<TokenService>();
        services.AddScoped<ITokenService>(sp => sp.GetRequiredService<TokenService>());
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        
        // Register Dapr service invocation helper
        services.AddScoped<IDaprServiceInvocation, DaprServiceInvocation>();

        return services;
    }

    public static async Task SeedRolesAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRoleIdentity>>();

        foreach (var roleName in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new ApplicationRoleIdentity(roleName)
                {
                    Description = $"{roleName} role for the e-commerce platform"
                });
            }
        }
    }
}

