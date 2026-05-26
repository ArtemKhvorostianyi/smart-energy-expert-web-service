using Ivy.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace SmartEnergyExpert.Client.Services.Auth;

/// <summary>
/// Basic Auth: облікові записи PostgreSQL + гість. Не успадковує <see cref="BasicAuthProvider"/>,
/// бо його публічний LoginAsync перехоплює виклики через <see cref="IAuthProvider"/>.
/// </summary>
public sealed class AppBasicAuthProvider : BasicAuthTokenHandler, IAuthProvider
{
    private readonly UserAccountService _users;
    private readonly IConfiguration _configuration;

    public AppBasicAuthProvider(IConfiguration configuration, UserAccountService users)
        : base(configuration)
    {
        _configuration = configuration;
        _users = users;
    }

    public async Task<LoginResult> LoginAsync(
        IAuthSession authSession,
        string user,
        string password,
        CancellationToken cancellationToken)
    {
        var login = user.Trim();
        if (IsGuestLogin(login, password))
        {
            return SuccessToken(GuestCredentials.Username);
        }

        var dbUser = await _users.ValidateCredentialsAsync(login, password, cancellationToken);
        if (dbUser is not null)
        {
            return SuccessToken(dbUser.Email);
        }

        return LoginResult.InvalidCredentials();
    }

    public Task LogoutAsync(IAuthSession authSession, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<Uri> GetOAuthUriAsync(
        IAuthSession authSession,
        AuthOption option,
        WebhookEndpoint callback,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<AuthToken?> HandleOAuthCallbackAsync(
        IAuthSession authSession,
        HttpRequest request,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public AuthOption[] GetAuthOptions() =>
        [new AuthOption(AuthFlow.EmailPassword)];

    private LoginResult SuccessToken(string userId)
    {
        var now = DateTimeOffset.UtcNow;
        return LoginResult.Success(CreateToken(userId, now, now.ToUnixTimeSeconds()));
    }

    private bool IsGuestLogin(string login, string password) =>
        string.Equals(login, GuestCredentials.Username, StringComparison.OrdinalIgnoreCase)
        && password == GuestCredentials.ResolvePassword(_configuration);
}
