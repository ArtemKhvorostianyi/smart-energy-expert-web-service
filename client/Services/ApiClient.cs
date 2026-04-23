using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace SmartEnergyExpert.Client.Services;

public interface IApiClient
{
    Task<IReadOnlyList<ExperimentDto>> GetExperimentsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExperimentParameterDto>> GetParametersAsync(Guid experimentId, CancellationToken cancellationToken = default);
    Task<EvaluationResultDto?> GetLatestEvaluationAsync(Guid experimentId, CancellationToken cancellationToken = default);
    Task<ExperimentDto> CreateExperimentAsync(CreateExperimentRequestDto request, CancellationToken cancellationToken = default);
    Task AddParameterAsync(Guid experimentId, AddParameterRequestDto request, CancellationToken cancellationToken = default);
    Task<EvaluationResultDto> EvaluateAsync(Guid experimentId, string? conclusion, CancellationToken cancellationToken = default);
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
        var baseUrl = configuration["BackendApi:BaseUrl"]
            ?? Environment.GetEnvironmentVariable("SEE_API_BASE_URL")
            ?? "http://localhost:5010/";
        _backendEmail = configuration["BackendApi:Email"]
            ?? Environment.GetEnvironmentVariable("SEE_BACKEND_EMAIL")
            ?? "admin@smartenergy.local";
        _backendPassword = configuration["BackendApi:Password"]
            ?? Environment.GetEnvironmentVariable("SEE_BACKEND_PASSWORD")
            ?? "Admin123!";

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl)
        };
    }

    public async Task<IReadOnlyList<ExperimentDto>> GetExperimentsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureBackendAuthorizedAsync(cancellationToken);
        var data = await _httpClient.GetFromJsonAsync<List<ExperimentDto>>("api/experiments", JsonOptions, cancellationToken);
        return data ?? [];
    }

    public async Task<ExperimentDto> CreateExperimentAsync(CreateExperimentRequestDto request, CancellationToken cancellationToken = default)
    {
        await EnsureBackendAuthorizedAsync(cancellationToken);
        var response = await _httpClient.PostAsJsonAsync("api/experiments", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ExperimentDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Create experiment response payload is empty.");
    }

    public async Task<IReadOnlyList<ExperimentParameterDto>> GetParametersAsync(Guid experimentId, CancellationToken cancellationToken = default)
    {
        await EnsureBackendAuthorizedAsync(cancellationToken);
        var data = await _httpClient.GetFromJsonAsync<List<ExperimentParameterDto>>(
            $"api/experiments/{experimentId}/parameters",
            JsonOptions,
            cancellationToken);
        return data ?? [];
    }

    public async Task<EvaluationResultDto?> GetLatestEvaluationAsync(Guid experimentId, CancellationToken cancellationToken = default)
    {
        await EnsureBackendAuthorizedAsync(cancellationToken);

        var response = await _httpClient.GetAsync($"api/experiments/{experimentId}/evaluation/latest", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EvaluationResultDto>(JsonOptions, cancellationToken);
    }

    public async Task AddParameterAsync(Guid experimentId, AddParameterRequestDto request, CancellationToken cancellationToken = default)
    {
        await EnsureBackendAuthorizedAsync(cancellationToken);
        var response = await _httpClient.PostAsJsonAsync($"api/experiments/{experimentId}/parameters", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<EvaluationResultDto> EvaluateAsync(Guid experimentId, string? conclusion, CancellationToken cancellationToken = default)
    {
        await EnsureBackendAuthorizedAsync(cancellationToken);
        var response = await _httpClient.PostAsJsonAsync(
            $"api/experiments/{experimentId}/evaluation",
            new EvaluationRequestDto { Conclusion = conclusion?.Trim() },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<EvaluationResultDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Evaluation response payload is empty.");
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
                new LoginRequestDto
                {
                    Email = _backendEmail,
                    Password = _backendPassword
                },
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<LoginResponseDto>(JsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("Backend login response payload is empty.");

            _accessToken = payload.AccessToken;
            _accessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresInSeconds);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payload.AccessToken);
        }
        catch (Exception ex)
        {
            var details = ex.GetBaseException().Message;
            throw new InvalidOperationException(
                "Unable to authenticate against backend API. Check BackendApi:BaseUrl / credentials and ensure backend + PostgreSQL are running. Root cause: " + details,
                ex);
        }
        finally
        {
            _authLock.Release();
        }
    }
}

