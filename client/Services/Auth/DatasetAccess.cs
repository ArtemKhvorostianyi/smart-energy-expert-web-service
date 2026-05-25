using SmartEnergyExpert.Client.Entities;

namespace SmartEnergyExpert.Client.Services.Auth;

public sealed record DatasetAccessContext(bool IsGuest, Guid? UserId)
{
    public static DatasetAccessContext Guest { get; } = new(true, null);
}

/// <summary>
/// Область видимості датасетів. <see cref="IAuthService"/> існує лише в app-session Ivy,
/// тому контекст збирається у View і передається в <see cref="Services.IApiClient"/>.
/// </summary>
public static class DatasetAccess
{
    public static async Task<DatasetAccessContext> ResolveAsync(
        IAuthService auth,
        UserAccountService users,
        CancellationToken cancellationToken = default)
    {
        if (auth.GetAuthSession()?.AuthToken is null)
        {
            return DatasetAccessContext.Guest;
        }

        UserInfo? info = null;
        try
        {
            info = await auth.GetUserInfoAsync(cancellationToken);
        }
        catch
        {
            // ignore
        }

        return await ResolveFromUserInfoAsync(info, users, cancellationToken);
    }

    public static async Task<DatasetAccessContext> ResolveFromUserInfoAsync(
        UserInfo? info,
        UserAccountService users,
        CancellationToken cancellationToken = default)
    {
        var key = info?.Email ?? info?.Id;
        if (AuthAccess.IsGuestEmail(key) || string.IsNullOrWhiteSpace(key))
        {
            return DatasetAccessContext.Guest;
        }

        var user = await users.FindByEmailAsync(key, cancellationToken);
        return user is null
            ? DatasetAccessContext.Guest
            : new DatasetAccessContext(IsGuest: false, UserId: user.Id);
    }

    public static IQueryable<Dataset> ApplyScope(IQueryable<Dataset> query, DatasetAccessContext access) =>
        access.IsGuest
            ? query.Where(x => x.IsGuestCatalog)
            : query.Where(x => x.IsGuestCatalog || x.OwnerUserId == access.UserId);

    public static bool CanAccess(Dataset dataset, DatasetAccessContext access) =>
        dataset.IsGuestCatalog
        || (!access.IsGuest && dataset.OwnerUserId == access.UserId);
}
