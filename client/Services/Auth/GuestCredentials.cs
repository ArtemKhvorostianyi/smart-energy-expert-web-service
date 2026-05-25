namespace SmartEnergyExpert.Client.Services.Auth;

public static class GuestCredentials
{
    public const string Username = "guest";
    public const string Email = "guest@explore.local";

    public static string ResolvePassword(IConfiguration configuration) =>
        configuration["Guest:Password"] ?? "explore";
}
