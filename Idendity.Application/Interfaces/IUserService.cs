using Idendity.Application.DTOs.Auth;
using Idendity.Application.DTOs.Common;

namespace Idendity.Application.Interfaces;

/// <summary>
/// User management service interface
/// </summary>
public interface IUserService
{
    Task<Result<UserDto>> GetByIdAsync(Guid userId);
    Task<Result<UserDto>> GetCurrentUserAsync(Guid userId);
    Task<Result<IEnumerable<UserDto>>> GetAllAsync(int page = 1, int pageSize = 20);
    Task<Result> UpdateAsync(Guid userId, UpdateUserRequest request);
    Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
    Task<Result> DeactivateAsync(Guid userId);
}

public record UpdateUserRequest(
    string? FirstName = null,
    string? LastName = null,
    string? PhoneNumber = null
);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword
);


