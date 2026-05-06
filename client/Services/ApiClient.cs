using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace SmartEnergyExpert.Client.Services;

public interface IApiClient
{
    Task<IReadOnlyList<DatasetDto>> GetDatasetsAsync(CancellationToken cancellationToken = default);
    Task<DatasetDto> CreateDatasetAsync(CreateDatasetRequestDto request, CancellationToken cancellationToken = default);
    Task<int> ImportCsvSamplesAsync(Guid datasetId, string csvContent, CancellationToken cancellationToken = default);
    Task<int> ImportCsvFileAsync(Guid datasetId, string filePath, CancellationToken cancellationToken = default);
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

    public async Task<DatasetDto> CreateDatasetAsync(CreateDatasetRequestDto request, CancellationToken cancellationToken = default)
    {
        await EnsureBackendAuthorizedAsync(cancellationToken);
        var response = await _httpClient.PostAsJsonAsync("api/datasets", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DatasetDto>(JsonOptions, cancellationToken)
               ?? throw new InvalidOperationException("Create dataset response payload is empty.");
    }

    public async Task<int> ImportCsvSamplesAsync(Guid datasetId, string csvContent, CancellationToken cancellationToken = default)
    {
        await EnsureBackendAuthorizedAsync(cancellationToken);
        using var content = new StringContent(csvContent, Encoding.UTF8, "text/plain");
        var response = await _httpClient.PostAsync($"api/datasets/{datasetId}/samples/import-csv", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, int>>(JsonOptions, cancellationToken);
        return payload is not null && payload.TryGetValue("imported", out var imported) ? imported : 0;
    }

    public async Task<int> ImportCsvFileAsync(Guid datasetId, string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("File path is empty.");
        }

        await EnsureBackendAuthorizedAsync(cancellationToken);
        await using var fileStream = File.OpenRead(filePath.Trim());
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", Path.GetFileName(filePath.Trim()));

        var response = await _httpClient.PostAsync($"api/datasets/{datasetId}/samples/import-csv-file", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, int>>(JsonOptions, cancellationToken);
        return payload is not null && payload.TryGetValue("imported", out var imported) ? imported : 0;
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
