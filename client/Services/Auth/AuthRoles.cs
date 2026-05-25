namespace SmartEnergyExpert.Client.Services.Auth;

public static class AuthRoles
{
    public const string Analyst = "analyst";
    public const string Guest = "guest";

    public static bool IsGuestRole(string? roleName) =>
        string.Equals(roleName, Guest, StringComparison.OrdinalIgnoreCase);
}
