using SmartEnergyExpert.Client.Services.Auth;

namespace SmartEnergyExpert.Client.Apps;

[App(
    icon: Icons.User,
    title: "Профіль",
    group: ["Обліковий запис"],
    order: 5,
    searchHints: ["профіль", "вихід", "logout", "account", "користувач"])]
public sealed class AccountApp : ViewBase
{
    public override object? Build()
    {
        var auth = UseService<IAuthService>();
        var navigator = UseNavigation();
        var status = UseState("");
        var userQuery = UseQuery(
            key: AuthViewHelper.UserQueryKey,
            fetcher: async ct =>
            {
                if (auth.GetAuthSession()?.AuthToken is null)
                {
                    return (UserInfo?)null;
                }

                return auth is AuthService authService
                    ? await authService.GetUserInfoAsync(ct)
                    : null;
            });
        var access = AuthAccess.From(auth, userQuery.Value);

        if (!access.IsAuthenticated)
        {
            return Layout.Vertical().Gap(2)
                   | Text.H2("Профіль")
                   | Callout.Info("Ви не увійшли. Увійдіть, зареєструйте профіль або продовжіть як гість.")
                   | new Button("Увійти / зареєструватися")
                       .Primary()
                       .OnClick(() => navigator.Navigate(typeof(AuthApp)));
        }

        return Layout.Vertical().Gap(2)
               | Text.H2("Профіль")
               | new Card(
                   Layout.Vertical().Gap(1)
                   | Text.Block($"Email: {access.Email ?? "—"}").Bold()
                   | Text.Block($"Ім’я: {access.FullName ?? "—"}")
                   | Text.Block(access.IsGuest
                       ? "Режим: гість (обмежений перегляд)"
                       : "Режим: повний доступ")
                   | (access.IsGuest
                       ? new Button("Увійти з email / зареєструватися")
                           .OnClick(() => navigator.Navigate(typeof(AuthApp)))
                       : new Fragment())
                   | new Button("Вийти")
                       .OnClick(async () =>
                       {
                           await auth.LogoutAsync();
                           status.Set("Сесію завершено. Доступні лише функції для гостя.");
                       }))
               | (string.IsNullOrWhiteSpace(status.Value) ? new Fragment() : Callout.Info(status.Value));
    }
}
