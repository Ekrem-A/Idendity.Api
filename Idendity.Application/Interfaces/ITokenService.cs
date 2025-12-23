using Idendity.Domain.Entities;

namespace Idendity.Application.Interfaces;

/// <summary>
/// JWT token generation service interface
/// </summary>
public interface ITokenService
{
    string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles);
    RefreshToken GenerateRefreshToken(Guid userId, string? ipAddress = null, string? deviceInfo = null);
    bool ValidateAccessToken(string token);
}


