using Idendity.Application.DTOs.Auth;
using Idendity.Application.DTOs.Common;
using Idendity.Application.Interfaces;
using Idendity.Domain.Constants;
using Idendity.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Idendity.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUserIdentity> _userManager;
    private readonly SignInManager<ApplicationUserIdentity> _signInManager;
    private readonly TokenService _tokenService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUserIdentity> userManager,
        SignInManager<ApplicationUserIdentity> signInManager,
        TokenService tokenService,
        IRefreshTokenRepository refreshTokenRepository,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _refreshTokenRepository = refreshTokenRepository;
        _logger = logger;
    }

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, string? ipAddress = null)
    {
        // Check if user already exists
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return Result<AuthResponse>.Failure("A user with this email already exists");
        }

        var user = new ApplicationUserIdentity
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            EmailConfirmed = true, // For demo; in production, require email confirmation
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description);
            _logger.LogWarning("User registration failed for {Email}: {Errors}", request.Email, string.Join(", ", errors));
            return Result<AuthResponse>.Failure(errors);
        }

        // Assign default role
        await _userManager.AddToRoleAsync(user, Roles.Customer);

        _logger.LogInformation("User registered successfully: {Email}", request.Email);

        // Generate tokens
        return await GenerateAuthResponseAsync(user, ipAddress, request.DeviceInfo);
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, string? ipAddress = null)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            _logger.LogWarning("Login attempt for non-existent user: {Email}", request.Email);
            return Result<AuthResponse>.Failure("Invalid email or password");
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Login attempt for deactivated user: {Email}", request.Email);
            return Result<AuthResponse>.Failure("Account is deactivated");
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        
        if (result.IsLockedOut)
        {
            _logger.LogWarning("User account locked out: {Email}", request.Email);
            return Result<AuthResponse>.Failure("Account is locked. Please try again later.");
        }

        if (!result.Succeeded)
        {
            _logger.LogWarning("Invalid password for user: {Email}", request.Email);
            return Result<AuthResponse>.Failure("Invalid email or password");
        }

        _logger.LogInformation("User logged in successfully: {Email}", request.Email);

        return await GenerateAuthResponseAsync(user, ipAddress, request.DeviceInfo);
    }

    public async Task<Result<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request, string? ipAddress = null)
    {
        var existingToken = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken);
        
        if (existingToken == null)
        {
            _logger.LogWarning("Refresh token not found");
            return Result<AuthResponse>.Failure("Invalid refresh token");
        }

        if (existingToken.IsRevoked)
        {
            // Token reuse detected - revoke all tokens for security
            _logger.LogWarning("Refresh token reuse detected for user {UserId}", existingToken.UserId);
            await _refreshTokenRepository.RevokeAllUserTokensAsync(existingToken.UserId, "Token reuse detected");
            await _refreshTokenRepository.SaveChangesAsync();
            return Result<AuthResponse>.Failure("Token has been revoked. Please login again.");
        }

        if (existingToken.IsExpired)
        {
            _logger.LogWarning("Expired refresh token used for user {UserId}", existingToken.UserId);
            return Result<AuthResponse>.Failure("Refresh token has expired. Please login again.");
        }

        var user = await _userManager.FindByIdAsync(existingToken.UserId.ToString());
        if (user == null || !user.IsActive)
        {
            return Result<AuthResponse>.Failure("User not found or inactive");
        }

        // Revoke the old token
        existingToken.RevokedAt = DateTime.UtcNow;
        existingToken.ReasonRevoked = "Replaced by new token";
        
        // Generate new tokens
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _tokenService.GenerateAccessToken(user, roles);
        var newRefreshToken = _tokenService.GenerateRefreshToken(user.Id, ipAddress);
        
        existingToken.ReplacedByToken = newRefreshToken.Token;
        
        await _refreshTokenRepository.UpdateAsync(existingToken);
        await _refreshTokenRepository.AddAsync(newRefreshToken);
        await _refreshTokenRepository.SaveChangesAsync();

        _logger.LogInformation("Token refreshed successfully for user {UserId}", user.Id);

        return Result<AuthResponse>.Success(new AuthResponse(
            accessToken,
            newRefreshToken.Token,
            DateTime.UtcNow.AddMinutes(15),
            MapToUserDto(user, roles)
        ));
    }

    public async Task<Result> RevokeTokenAsync(Guid userId, RevokeTokenRequest request)
    {
        if (request.RevokeAll)
        {
            await _refreshTokenRepository.RevokeAllUserTokensAsync(userId, "User requested logout from all devices");
            await _refreshTokenRepository.SaveChangesAsync();
            _logger.LogInformation("All tokens revoked for user {UserId}", userId);
            return Result.Success();
        }

        if (string.IsNullOrEmpty(request.RefreshToken))
        {
            return Result.Failure("Refresh token is required");
        }

        var token = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken);
        if (token == null || token.UserId != userId)
        {
            return Result.Failure("Invalid refresh token");
        }

        token.RevokedAt = DateTime.UtcNow;
        token.ReasonRevoked = "User requested revocation";
        await _refreshTokenRepository.UpdateAsync(token);
        await _refreshTokenRepository.SaveChangesAsync();

        _logger.LogInformation("Token revoked for user {UserId}", userId);
        return Result.Success();
    }

    public async Task<Result> LogoutAsync(Guid userId, string? refreshToken = null)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            // Revoke all tokens
            await _refreshTokenRepository.RevokeAllUserTokensAsync(userId, "User logged out");
        }
        else
        {
            var token = await _refreshTokenRepository.GetByTokenAsync(refreshToken);
            if (token != null && token.UserId == userId && !token.IsRevoked)
            {
                token.RevokedAt = DateTime.UtcNow;
                token.ReasonRevoked = "User logged out";
                await _refreshTokenRepository.UpdateAsync(token);
            }
        }

        await _refreshTokenRepository.SaveChangesAsync();
        _logger.LogInformation("User logged out: {UserId}", userId);
        return Result.Success();
    }

    private async Task<Result<AuthResponse>> GenerateAuthResponseAsync(
        ApplicationUserIdentity user, 
        string? ipAddress, 
        string? deviceInfo = null)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _tokenService.GenerateAccessToken(user, roles);
        var refreshToken = _tokenService.GenerateRefreshToken(user.Id, ipAddress, deviceInfo);

        await _refreshTokenRepository.AddAsync(refreshToken);
        await _refreshTokenRepository.SaveChangesAsync();

        return Result<AuthResponse>.Success(new AuthResponse(
            accessToken,
            refreshToken.Token,
            DateTime.UtcNow.AddMinutes(15),
            MapToUserDto(user, roles)
        ));
    }

    private static UserDto MapToUserDto(ApplicationUserIdentity user, IEnumerable<string> roles)
    {
        return new UserDto(
            user.Id,
            user.Email ?? string.Empty,
            user.FirstName,
            user.LastName,
            user.FullName,
            roles
        );
    }
}


