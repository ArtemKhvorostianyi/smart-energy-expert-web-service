using SmartEnergyExpert.Client.Services.Auth;

namespace SmartEnergyExpert.Client.Apps;

/// <summary>Сторінка входу / реєстрації / гостьового доступу (Ivy Basic Auth).</summary>
[App(
    icon: Icons.LogIn,
    title: "Вхід",
    group: ["Обліковий запис"],
    order: 0,
    searchHints: ["вхід", "login", "реєстрація", "пароль", "guest", "auth"])]
public sealed class AuthApp : ViewBase
{
    public override object? Build()
    {
        var auth = UseService<IAuthService>();
        var configuration = UseService<IConfiguration>();
        var accounts = UseService<UserAccountService>();
        var mode = UseState("login");
        var status = UseState("");
        var busy = UseState(false);

        var loginEmail = UseState("");
        var loginPassword = UseState("");

        var regEmail = UseState("");
        var regName = UseState("");
        var regPassword = UseState("");
        var regPassword2 = UseState("");

        if (auth.GetAuthSession()?.AuthToken is not null)
        {
            return Layout.Vertical().Gap(2)
                   | Text.H2("Ви вже увійшли")
                   | Text.Muted("Перейдіть до «Профіль» для виходу або до розділів «Сервіс».")
                   | Callout.Info("Після входу оновіть вкладки — з’явиться повний доступ.");
        }

        object body = mode.Value == "register"
            ? BuildRegister(accounts, regEmail, regName, regPassword, regPassword2, mode, status, busy)
            : BuildLogin(auth, configuration, loginEmail, loginPassword, mode, status, busy);

        return Layout.Vertical().Gap(2)
               | Text.H2("Обліковий запис")
               | Text.Muted(
                   "Повний доступ: PDF-звіт, середовищна симуляція, імпорт CSV. "
                   + "Без входу — перегляд панелі, огляд сигналів і запуск порівняння на наявних датасетах.")
               | new Card(body)
               | (string.IsNullOrWhiteSpace(status.Value) ? new Fragment() : Callout.Info(status.Value));
    }

    private static object BuildLogin(
        IAuthService auth,
        IConfiguration configuration,
        IState<string> email,
        IState<string> password,
        IState<string> mode,
        IState<string> status,
        IState<bool> busy)
    {
        var guestPassword = GuestCredentials.ResolvePassword(configuration);
        return Layout.Vertical().Gap(2)
               | Text.H3("Вхід")
               | email.ToTextInput().Placeholder("Email")
               | password.ToPasswordInput().Placeholder("Пароль")
               | new Button("Увійти").Primary().Disabled(busy.Value).OnClick(async () =>
               {
                   busy.Set(true);
                   status.Set("");
                   try
                   {
                       var loginResult = await auth.LoginAsync(
                           email.Value.Trim().ToLowerInvariant(),
                           password.Value);
                       status.Set(loginResult?.Token is null
                           ? "Невірний email або пароль."
                           : "Вхід виконано. Відкрийте «Профіль» або розділи сервісу.");
                   }
                   catch (Exception ex)
                   {
                       status.Set($"Помилка входу: {ex.Message}");
                   }
                   finally
                   {
                       busy.Set(false);
                   }
               })
               | new Button("Продовжити як гість (обмежений режим)")
                   .OnClick(async () =>
                   {
                       busy.Set(true);
                       try
                       {
                           var loginResult = await auth.LoginAsync(GuestCredentials.Username, guestPassword);
                           status.Set(loginResult?.Token is null
                               ? "Гостьовий вхід недоступний."
                               : "Гостьовий режим увімкнено (перегляд без імпорту, симуляцій і PDF).");
                       }
                       catch (Exception ex)
                       {
                           status.Set(ex.Message);
                       }
                       finally
                       {
                           busy.Set(false);
                       }
                   })
               | Text.Muted($"Гість: {GuestCredentials.Username} / {guestPassword}")
               | new Button("Створити профіль").OnClick(() => { mode.Set("register"); status.Set(""); });
    }

    private static object BuildRegister(
        UserAccountService accounts,
        IState<string> email,
        IState<string> name,
        IState<string> password,
        IState<string> password2,
        IState<string> mode,
        IState<string> status,
        IState<bool> busy) =>
        Layout.Vertical().Gap(2)
        | Text.H3("Новий профіль")
        | email.ToTextInput().Placeholder("Email")
        | name.ToTextInput().Placeholder("Повне ім’я")
        | password.ToPasswordInput().Placeholder("Пароль (мін. 8 символів)")
        | password2.ToPasswordInput().Placeholder("Повтор пароля")
        | new Button("Зареєструватися").Primary().Disabled(busy.Value).OnClick(async () =>
        {
            if (password.Value != password2.Value)
            {
                status.Set("Паролі не збігаються.");
                return;
            }

            busy.Set(true);
            try
            {
                var (ok, message) = await accounts.RegisterAsync(email.Value, name.Value, password.Value);
                status.Set(message);
                if (ok)
                {
                    mode.Set("login");
                }
            }
            finally
            {
                busy.Set(false);
            }
        })
        | new Button("Назад до входу").OnClick(() => { mode.Set("login"); status.Set(""); });
}
