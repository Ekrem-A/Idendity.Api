using System.Diagnostics;
using System.Security.Claims;

namespace Idendity.Api.Middleware;

/// <summary>
/// Middleware for audit logging of API requests
/// </summary>
public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;

    public AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestId = Guid.NewGuid().ToString("N")[..8];
        
        // Get user info if authenticated
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        var userEmail = context.User?.FindFirst(ClaimTypes.Email)?.Value ?? "N/A";
        
        // Get request info
        var method = context.Request.Method;
        var path = context.Request.Path;
        var ipAddress = GetClientIpAddress(context);
        var userAgent = context.Request.Headers.UserAgent.FirstOrDefault() ?? "Unknown";

        // Log request start
        _logger.LogInformation(
            "Request {RequestId} started: {Method} {Path} | User: {UserId} | IP: {IpAddress}",
            requestId, method, path, userId, ipAddress);

        try
        {
            await _next(context);
            
            stopwatch.Stop();
            
            // Log request completion
            _logger.LogInformation(
                "Request {RequestId} completed: {Method} {Path} | Status: {StatusCode} | Duration: {Duration}ms | User: {UserId}",
                requestId, method, path, context.Response.StatusCode, stopwatch.ElapsedMilliseconds, userId);

            // Log security-sensitive operations with more detail
            if (IsSecuritySensitiveEndpoint(path))
            {
                _logger.LogWarning(
                    "AUDIT: {Method} {Path} | Status: {StatusCode} | User: {UserId} ({Email}) | IP: {IpAddress} | UserAgent: {UserAgent}",
                    method, path, context.Response.StatusCode, userId, userEmail, ipAddress, userAgent);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(ex,
                "Request {RequestId} failed: {Method} {Path} | Duration: {Duration}ms | User: {UserId} | Error: {Error}",
                requestId, method, path, stopwatch.ElapsedMilliseconds, userId, ex.Message);
            
            throw;
        }
    }

    private static string? GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded IP (when behind a proxy/load balancer)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }

    private static bool IsSecuritySensitiveEndpoint(PathString path)
    {
        var sensitiveEndpoints = new[]
        {
            "/api/auth/login",
            "/api/auth/register",
            "/api/auth/refresh",
            "/api/auth/revoke",
            "/api/auth/logout",
            "/api/users/me/change-password"
        };

        return sensitiveEndpoints.Any(e => 
            path.StartsWithSegments(e, StringComparison.OrdinalIgnoreCase));
    }
}

public static class AuditLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseAuditLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AuditLoggingMiddleware>();
    }
}


