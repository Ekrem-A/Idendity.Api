namespace Idendity.Application.DTOs.Auth;

public record LoginRequest(
    string Email,
    string Password,
    string? DeviceInfo = null
);


