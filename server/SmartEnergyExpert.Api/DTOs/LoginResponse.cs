namespace SmartEnergyExpert.Api.DTOs;

public sealed class LoginResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public int ExpiresInSeconds { get; init; }
    public Guid UserId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
}
