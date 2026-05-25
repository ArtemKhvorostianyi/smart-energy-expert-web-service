namespace SmartEnergyExpert.Client.Services.Auth;

/// <summary>Guest = no session or guest role. Write = signed-in analyst (not guest).</summary>
public sealed record AuthAccessState(bool IsAuthenticated, bool IsGuest, bool CanWrite, string? Email, string? FullName);

public static class AuthAccess
{
    public static AuthAccessState From(IAuthService auth, UserInfo? user)
    {
        var session = auth.GetAuthSession();
        var hasToken = session?.AuthToken is not null;
        if (!hasToken)
        {
            return new AuthAccessState(false, true, false, null, null);
        }

        var email = user?.Email;
        var isGuest = IsGuestEmail(email) || IsGuestEmail(user?.Id);
        return new AuthAccessState(true, isGuest, !isGuest, email, user?.FullName);
    }

    public static bool IsGuestEmail(string? value) =>
        string.Equals(value, GuestCredentials.Username, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, GuestCredentials.Email, StringComparison.OrdinalIgnoreCase);

    public static object RequireWriteGate(AuthAccessState access, string messageUk) =>
        access.CanWrite
            ? new Fragment()
            : Callout.Warning(messageUk);
}
