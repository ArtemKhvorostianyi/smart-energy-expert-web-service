using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmartEnergyExpert.Api.Data;
using SmartEnergyExpert.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// У Docker/Sliplane healthcheck зазвичай http://127.0.0.1:$PORT/. Kestrel з http://+:8080 часто
// показує лише [::]:8080; на частині Linux 127.0.0.1 не потрапляє на той самий listen — «мертвий» сервіс.
// Плюс Sliplane може задати PORT ≠ 8080. У контейнері слухаємо 0.0.0.0 і PORT або 8080.
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

var jwtKey = builder.Configuration["Jwt:Key"] ?? "dev-only-super-secret-key-change-this";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "SmartEnergyExpert.Api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "SmartEnergyExpert.Client";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddScoped<IComparisonService, ComparisonService>();
builder.Services.AddScoped<IParameterSyntheticSimulationService, ParameterSyntheticSimulationService>();
builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

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

// TLS завершується на Sliplane / іншому edge; Kestrel лише HTTP — UseHttpsRedirection дає
// "Failed to determine the https port" і не потрібен для цього API.
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// HEAD — для проб; GET у браузері: короткий текст (раніше був порожній 200 → «біла сторінка»).
static IResult Liveness(HttpRequest request)
{
    if (HttpMethods.IsHead(request.Method))
        return Results.Ok();
    return Results.Text(
        "SmartEnergyExpert API (backend only).\n\n"
        + "JSON: /api/...\n"
        + "UI: Ivy client locally — BackendApi:BaseUrl → this host (HTTPS).\n",
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
                + "Host має бути internal hostname сервісу Postgres у Sliplane (див. Settings → Service Info).");
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
