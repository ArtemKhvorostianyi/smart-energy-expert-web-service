using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmartEnergyExpert.Api.Data;
using SmartEnergyExpert.Api.Services;

var builder = WebApplication.CreateBuilder(args);

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

app.Run();

static string ResolveNpgsqlConnectionString(WebApplicationBuilder builder)
{
    var conn = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrWhiteSpace(conn))
    {
        return conn;
    }

    if (builder.Environment.IsProduction())
    {
        throw new InvalidOperationException(
            "У Production потрібен PostgreSQL. Задай змінну середовища ConnectionStrings__DefaultConnection "
            + "(повний рядок Npgsql). У Docker Host не може бути localhost — використай hostname сервісу Postgres у Sliplane.");
    }

    return "Host=localhost;Port=5432;Database=hydroacoustic_expert;Username=postgres;Password=postgres";
}
