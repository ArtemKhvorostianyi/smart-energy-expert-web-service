namespace SmartEnergyExpert.Client.Services.Auth;

public static class AuthViewHelper
{
    public const string UserQueryKey = "auth-current-user";

    public const string WriteLockedMessage =
        "Повний доступ після входу (не гість): імпорт CSV, симуляції, PDF-звіт. Розділ «Вхід» → зареєструйте профіль або увійдіть.";

    public static Task<DatasetAccessContext> ResolveDatasetScopeAsync(
        IAuthService auth,
        UserAccountService users,
        UserInfo? userInfo,
        CancellationToken cancellationToken = default) =>
        userInfo is not null && auth.GetAuthSession()?.AuthToken is not null
            ? DatasetAccess.ResolveFromUserInfoAsync(userInfo, users, cancellationToken)
            : DatasetAccess.ResolveAsync(auth, users, cancellationToken);
}
