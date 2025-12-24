using AspNetCoreRateLimit;
using Idendity.Api.Middleware;
using Idendity.Application;
using Idendity.Infrastructure;
using Idendity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Railway sets PORT for inbound traffic. If present, listen on that port.
var appPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(appPort))
{
    builder.WebHost.UseUrls($"http://+:{appPort}");
}

// Railway commonly provides DATABASE_URL for PostgreSQL. If present, we prefer it and convert to an
// Npgsql-compatible connection string (this overrides appsettings.json).
{
    var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (!string.IsNullOrWhiteSpace(databaseUrl))
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2);
        var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "";
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var host = uri.Host;
        var dbPort = uri.Port > 0 ? uri.Port : 5432;
        var database = uri.AbsolutePath.TrimStart('/');

        builder.Configuration["ConnectionStrings:DefaultConnection"] =
            $"Host={host};Port={dbPort};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true";
    }
}

// If DATABASE_URL is not set, we fall back to ConnectionStrings:DefaultConnection from config (appsettings/env vars).
if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("DefaultConnection")))
{
    throw new InvalidOperationException(
        "Database connection is not configured. Set DATABASE_URL (Railway) or ConnectionStrings:DefaultConnection.");
}

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "IdentityService")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .WriteTo.Console(outputTemplate: 
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with JWT authentication
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Identity API",
        Version = "v1",
        Description = "E-Commerce Identity Service API - Authentication and Authorization",
        Contact = new OpenApiContact
        {
            Name = "API Support"
        }
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer' [space] and then your valid token.\n\nExample: 'Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...'"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Add Application layer services
builder.Services.AddApplication();

// Add Infrastructure layer services (Identity, EF Core, JWT)
builder.Services.AddInfrastructure(builder.Configuration);

// Configure Rate Limiting
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.EnableEndpointRateLimiting = true;
    options.StackBlockedRequests = false;
    options.RealIpHeader = "X-Forwarded-For";
    options.ClientIdHeader = "X-ClientId";
    options.GeneralRules = new List<RateLimitRule>
    {
        new()
        {
            Endpoint = "POST:/api/auth/login",
            Period = "1m",
            Limit = 5 // 5 login attempts per minute
        },
        new()
        {
            Endpoint = "POST:/api/auth/register",
            Period = "1h",
            Limit = 10 // 10 registrations per hour per IP
        },
        new()
        {
            Endpoint = "*",
            Period = "1s",
            Limit = 10 // General rate limit
        }
    };
});
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
builder.Services.AddInMemoryRateLimiting();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:3000", "https://localhost:3000" };
        
        policy.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// Configure Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("DefaultConnection") ?? "",
        name: "postgres",
        tags: new[] { "db", "sql", "postgres" });

// Add Dapr client (for service-to-service communication)
builder.Services.AddDaprClient();

var app = builder.Build();

// Global exception handling (should be first)
app.UseExceptionHandling();

// Audit logging
app.UseAuditLogging();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Identity API v1");
        c.RoutePrefix = "swagger";
    });
}

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'";
    context.Response.Headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";
    await next();
});

app.UseHttpsRedirection();

app.UseCors("AllowSpecificOrigins");

app.UseIpRateLimiting();

app.UseAuthentication();
app.UseAuthorization();

// Cloud events for Dapr pub/sub
app.UseCloudEvents();

app.MapControllers();
app.MapSubscribeHandler(); // Dapr pub/sub endpoint

// Health check endpoints
app.MapHealthChecks("/health");

// Optional: run EF Core migrations automatically on startup (useful for Railway)
// Set RUN_MIGRATIONS=true in environment to enable.
if (string.Equals(Environment.GetEnvironmentVariable("RUN_MIGRATIONS"), "true", StringComparison.OrdinalIgnoreCase))
{
    using var migrationScope = app.Services.CreateScope();
    var db = migrationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    Log.Information("RUN_MIGRATIONS=true detected. Applying EF Core migrations...");
    await db.Database.MigrateAsync();
    Log.Information("EF Core migrations applied successfully.");
}

// Seed roles on startup
using (var scope = app.Services.CreateScope())
{
    try
    {
        await Idendity.Infrastructure.DependencyInjection.SeedRolesAsync(scope.ServiceProvider);
        Log.Information("Roles seeded successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred while seeding roles");
    }
}

Log.Information("Identity API starting up on {Environment} environment", app.Environment.EnvironmentName);

app.Run();
