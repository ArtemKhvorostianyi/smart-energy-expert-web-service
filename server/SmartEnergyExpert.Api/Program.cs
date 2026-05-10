using Microsoft.EntityFrameworkCore;
using SmartEnergyExpert.Api.Data;
using SmartEnergyExpert.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var inDotnetContainer = string.Equals(
    Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase);
var inLinuxDocker = OperatingSystem.IsLinux() && System.IO.File.Exists("/.dockerenv");
if (inDotnetContainer || inLinuxDocker)
{
    var portEnv = Environment.GetEnvironmentVariable("PORT");
    var listenPort = string.IsNullOrWhiteSpace(portEnv) ? "8080" : portEnv;
    builder.WebHost.UseUrls($"http://0.0.0.0:{listenPort}");
}

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var npgsqlConnectionString = ResolveNpgsqlConnectionString(builder);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(npgsqlConnectionString));

builder.Services.AddScoped<IComparisonService, ComparisonService>();
builder.Services.AddScoped<IParameterSyntheticSimulationService, ParameterSyntheticSimulationService>();
builder.Services.AddSingleton<DatabaseInitializer>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync(CancellationToken.None);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

static IResult Liveness(HttpRequest request)
{
    if (HttpMethods.IsHead(request.Method))
        return Results.Ok();
    return Results.Text(
        "SmartEnergyExpert API (without authentication).\n\n"
        + "JSON: /api/...\n"
        + "Ivy клієнт: налаштуйте BackendApi:BaseUrl у appsettings.json.\n",
        "text/plain; charset=utf-8");
}

app.MapMethods("/", new[] { "GET", "HEAD" }, Liveness);
app.MapMethods("/health", new[] { "GET", "HEAD" }, Liveness);

app.Run();

static string ResolveNpgsqlConnectionString(WebApplicationBuilder builder)
{
    var conn = builder.Configuration.GetConnectionString("DefaultConnection");

    if (builder.Environment.IsProduction())
    {
        if (string.IsNullOrWhiteSpace(conn) || LooksLikeLocalPostgresHost(conn))
        {
            throw new InvalidOperationException(
                "У Production потрібен PostgreSQL. Задай на сервісі API змінну ConnectionStrings__DefaultConnection "
                + "(повний рядок Npgsql). Значення з appsettings.json (localhost) у контейнері не підходить — "
                + "Host має бути internal hostname сервісу Postgres (напр. у Docker Compose — postgres).");
        }

        return conn;
    }

    if (!string.IsNullOrWhiteSpace(conn))
    {
        return conn;
    }

    return "Host=localhost;Port=5432;Database=hydroacoustic_expert;Username=postgres;Password=postgres";
}

static bool LooksLikeLocalPostgresHost(string connectionString)
{
    var s = connectionString.ToLowerInvariant();
    return s.Contains("host=localhost")
        || s.Contains("host=127.0.0.1")
        || s.Contains("server=localhost")
        || s.Contains("server=127.0.0.1");
}
