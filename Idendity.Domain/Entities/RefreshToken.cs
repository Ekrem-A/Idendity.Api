namespace Idendity.Domain.Entities;

/// <summary>
/// Refresh token entity for token rotation and revocation
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByToken { get; set; }
    public string? ReasonRevoked { get; set; }
    
    // Device/Session info for tracking
    public string? DeviceInfo { get; set; }
    public string? IpAddress { get; set; }
    
    // Navigation property
    public virtual ApplicationUser User { get; set; } = null!;
    
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt != null;
    public bool IsActive => !IsRevoked && !IsExpired;
}


