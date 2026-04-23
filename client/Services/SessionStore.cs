using System.Text.Json;

namespace SmartEnergyExpert.Client.Services;

public interface ISessionStore
{
    UserSession? Get();
    Task SaveAsync(UserSession session, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

public sealed class SessionStore : ISessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _sessionPath;
    private UserSession? _cached;

    public SessionStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "smart-energy-expert-client");
        Directory.CreateDirectory(dir);
        _sessionPath = Path.Combine(dir, "session.json");

        if (File.Exists(_sessionPath))
        {
            var json = File.ReadAllText(_sessionPath);
            _cached = JsonSerializer.Deserialize<UserSession>(json, JsonOptions);
        }
    }

    public UserSession? Get() => _cached;

    public async Task SaveAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        _cached = session;
        var json = JsonSerializer.Serialize(session, JsonOptions);
        await File.WriteAllTextAsync(_sessionPath, json, cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _cached = null;
        if (File.Exists(_sessionPath))
        {
            File.Delete(_sessionPath);
        }

        return Task.CompletedTask;
    }
}

