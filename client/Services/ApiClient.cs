using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace SmartEnergyExpert.Client.Services;

public interface IApiClient
{
    Task<IReadOnlyList<DatasetDto>> GetDatasetsAsync(CancellationToken cancellationToken = default);
    Task<ComparisonResultDto> RunComparisonAsync(CreateComparisonRequestDto request, CancellationToken cancellationToken = default);
}

public sealed class ApiClient : IApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _authLock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAt = DateTimeOffset.MinValue;
    private readonly string _backendEmail;
    private readonly string _backendPassword;

    public ApiClient(IConfiguration configuration)
    {
        var baseUrl = configuration["BackendApi:BaseUrl"] ?? "http://localhost:5109/";
        _backendEmail = configuration["BackendApi:Email"] ?? "admin@smartenergy.local";
        _backendPassword = configuration["BackendApi:Password"] ?? "Admin123!";
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    public async Task<IReadOnlyList<DatasetDto>> GetDatasetsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureBackendAuthorizedAsync(cancellationToken);
        var data = await _httpClient.GetFromJsonAsync<List<DatasetDto>>("api/datasets", JsonOptions, cancellationToken);
        return data ?? [];
    }

    public async Task<ComparisonResultDto> RunComparisonAsync(CreateComparisonRequestDto request, CancellationToken cancellationToken = default)
    {
        await EnsureBackendAuthorizedAsync(cancellationToken);
        var response = await _httpClient.PostAsJsonAsync("api/comparisons", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ComparisonResultDto>(JsonOptions, cancellationToken)
               ?? throw new InvalidOperationException("Comparison response payload is empty.");
    }

    private async Task EnsureBackendAuthorizedAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken) && _accessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return;
        }

        await _authLock.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(_accessToken) && _accessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            {
                return;
            }

            var response = await _httpClient.PostAsJsonAsync(
                "api/auth/login",
                new LoginRequestDto { Email = _backendEmail, Password = _backendPassword },
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<LoginResponseDto>(JsonOptions, cancellationToken)
                          ?? throw new InvalidOperationException("Backend login response payload is empty.");
            _accessToken = payload.AccessToken;
            _accessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresInSeconds);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payload.AccessToken);
        }
        finally
        {
            _authLock.Release();
        }
    }
}
