using AspNetCoreRateLimit;
using Idendity.Api.Middleware;
using Idendity.Application;
using Idendity.Infrastructure;
using Idendity.Infrastructure.Extensions;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add Azure Key Vault configuration if configured
builder.Configuration.AddAzureKeyVaultIfConfigured();

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
    .AddSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection") ?? "",
        name: "sqlserver",
        tags: new[] { "db", "sql", "sqlserver" });

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
