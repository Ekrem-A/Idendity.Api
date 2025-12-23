using Idendity.Domain.Entities;

namespace Idendity.Application.Interfaces;

/// <summary>
/// Repository interface for refresh token operations
/// </summary>
public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token);
    Task<IEnumerable<RefreshToken>> GetActiveTokensByUserIdAsync(Guid userId);
    Task AddAsync(RefreshToken token);
    Task UpdateAsync(RefreshToken token);
    Task RevokeAllUserTokensAsync(Guid userId, string reason);
    Task SaveChangesAsync();
}


