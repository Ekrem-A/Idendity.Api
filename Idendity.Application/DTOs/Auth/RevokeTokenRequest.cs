namespace Idendity.Application.DTOs.Auth;

public record RevokeTokenRequest(
    string? RefreshToken = null,
    bool RevokeAll = false
);


