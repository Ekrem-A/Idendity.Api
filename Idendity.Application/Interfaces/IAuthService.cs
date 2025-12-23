using Idendity.Application.DTOs.Auth;
using Idendity.Application.DTOs.Common;

namespace Idendity.Application.Interfaces;

/// <summary>
/// Authentication service interface
/// </summary>
public interface IAuthService
{
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, string? ipAddress = null);
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, string? ipAddress = null);
    Task<Result<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request, string? ipAddress = null);
    Task<Result> RevokeTokenAsync(Guid userId, RevokeTokenRequest request);
    Task<Result> LogoutAsync(Guid userId, string? refreshToken = null);
}


