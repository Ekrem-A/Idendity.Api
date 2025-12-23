using Idendity.Application.DTOs.Auth;
using Idendity.Application.DTOs.Common;
using Idendity.Application.Interfaces;
using Idendity.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Idendity.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly UserManager<ApplicationUserIdentity> _userManager;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ILogger<UserService> _logger;

    public UserService(
        UserManager<ApplicationUserIdentity> userManager,
        IRefreshTokenRepository refreshTokenRepository,
        ILogger<UserService> logger)
    {
        _userManager = userManager;
        _refreshTokenRepository = refreshTokenRepository;
        _logger = logger;
    }

    public async Task<Result<UserDto>> GetByIdAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Result<UserDto>.Failure("User not found");
        }

        var roles = await _userManager.GetRolesAsync(user);
        return Result<UserDto>.Success(MapToUserDto(user, roles));
    }

    public async Task<Result<UserDto>> GetCurrentUserAsync(Guid userId)
    {
        return await GetByIdAsync(userId);
    }

    public async Task<Result<IEnumerable<UserDto>>> GetAllAsync(int page = 1, int pageSize = 20)
    {
        var users = await _userManager.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var userDtos = new List<UserDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userDtos.Add(MapToUserDto(user, roles));
        }

        return Result<IEnumerable<UserDto>>.Success(userDtos);
    }

    public async Task<Result> UpdateAsync(Guid userId, UpdateUserRequest request)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Result.Failure("User not found");
        }

        if (!string.IsNullOrEmpty(request.FirstName))
            user.FirstName = request.FirstName;
        
        if (!string.IsNullOrEmpty(request.LastName))
            user.LastName = request.LastName;
        
        if (!string.IsNullOrEmpty(request.PhoneNumber))
            user.PhoneNumber = request.PhoneNumber;

        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description);
            return Result.Failure(errors);
        }

        _logger.LogInformation("User updated: {UserId}", userId);
        return Result.Success();
    }

    public async Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        if (request.NewPassword != request.ConfirmNewPassword)
        {
            return Result.Failure("New passwords do not match");
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Result.Failure("User not found");
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description);
            return Result.Failure(errors);
        }

        // Revoke all refresh tokens for security
        await _refreshTokenRepository.RevokeAllUserTokensAsync(userId, "Password changed");
        await _refreshTokenRepository.SaveChangesAsync();

        _logger.LogInformation("Password changed for user: {UserId}", userId);
        return Result.Success();
    }

    public async Task<Result> DeactivateAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Result.Failure("User not found");
        }

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description);
            return Result.Failure(errors);
        }

        // Revoke all refresh tokens
        await _refreshTokenRepository.RevokeAllUserTokensAsync(userId, "Account deactivated");
        await _refreshTokenRepository.SaveChangesAsync();

        _logger.LogInformation("User deactivated: {UserId}", userId);
        return Result.Success();
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


