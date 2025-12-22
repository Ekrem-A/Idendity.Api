namespace Idendity.Application.DTOs.Auth;

public record RegisterRequest(
    string Email,
    string Password,
    string ConfirmPassword,
    string FirstName,
    string LastName,
    string? PhoneNumber = null,
    string? DeviceInfo = null
);
