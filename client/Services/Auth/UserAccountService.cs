using Microsoft.EntityFrameworkCore;
using SmartEnergyExpert.Client.Data;
using SmartEnergyExpert.Client.Entities;

namespace SmartEnergyExpert.Client.Services.Auth;

public sealed class UserAccountService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<(bool Ok, string Message)> RegisterAsync(
        string email,
        string fullName,
        string password,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail.Length < 3 || !normalizedEmail.Contains('@'))
        {
            return (false, "Вкажіть коректну email-адресу.");
        }

        if (string.Equals(normalizedEmail, GuestCredentials.Email, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedEmail, GuestCredentials.Username, StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Цю адресу зарезервовано для гостьового доступу.");
        }

        if (password.Length < 8)
        {
            return (false, "Пароль має містити щонайменше 8 символів.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.Users.AnyAsync(x => x.Email == normalizedEmail, cancellationToken))
        {
            return (false, "Користувач з такою email-адресою вже існує.");
        }

        var role = await db.Roles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Name == AuthRoles.Analyst, cancellationToken);
        if (role is null)
        {
            role = new Role { Name = AuthRoles.Analyst };
            db.Roles.Add(role);
            await db.SaveChangesAsync(cancellationToken);
        }

        db.Users.Add(new User
        {
            Email = normalizedEmail,
            FullName = string.IsNullOrWhiteSpace(fullName) ? normalizedEmail : fullName.Trim(),
            PasswordHash = PasswordHasher.HashPassword(password),
            RoleId = role.Id,
            IsActive = true
        });
        await db.SaveChangesAsync(cancellationToken);
        return (true, "Профіль створено. Увійдіть email і паролем.");
    }

    public async Task<User?> ValidateCredentialsAsync(
        string login,
        string password,
        CancellationToken cancellationToken = default)
    {
        var key = login.Trim();
        if (key.Length == 0)
        {
            return null;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var normalized = key.Contains('@') ? NormalizeEmail(key) : key;
        var user = await db.Users
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.Email == normalized, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return null;
        }

        return PasswordHasher.Verify(password, user.PasswordHash) ? user : null;
    }

    public async Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeEmail(email);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Users.AsNoTracking().Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.Email == normalized, cancellationToken);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
