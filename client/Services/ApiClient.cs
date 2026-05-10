using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace SmartEnergyExpert.Client.Services;

public interface IApiClient
{
    Task<IReadOnlyList<DatasetDto>> GetDatasetsAsync(CancellationToken cancellationToken = default);
    Task<DatasetSignalOverviewDto?> GetDatasetSignalOverviewAsync(Guid datasetId, CancellationToken cancellationToken = default);
    Task<DatasetSamplesPageDto?> GetDatasetSamplesPageAsync(
        Guid datasetId,
        int offset = 0,
        int limit = 200,
        CancellationToken cancellationToken = default);
    Task<DatasetDto> CreateDatasetAsync(CreateDatasetRequestDto request, CancellationToken cancellationToken = default);
    Task<DatasetDto> GenerateSimulationDatasetAsync(
        GenerateSimulationDatasetRequestDto request,
        CancellationToken cancellationToken = default);
    Task DeleteDatasetAsync(Guid datasetId, CancellationToken cancellationToken = default);
    Task<int> ImportCsvSamplesAsync(Guid datasetId, string csvContent, CancellationToken cancellationToken = default);
    Task<int> ImportCsvFileMultipartAsync(Guid datasetId, byte[] utf8Csv, string fileName, CancellationToken cancellationToken = default);
    Task<int> ImportCsvFileAsync(Guid datasetId, string filePath, CancellationToken cancellationToken = default);
    Task<ComparisonResultDto> RunComparisonAsync(CreateComparisonRequestDto request, CancellationToken cancellationToken = default);
}

public sealed class ApiClient : IApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;

    public ApiClient(IConfiguration configuration)
    {
        var baseUrl = configuration["BackendApi:BaseUrl"] ?? "http://localhost:5109/";
        if (!baseUrl.EndsWith("/", StringComparison.Ordinal))
        {
            baseUrl += "/";
        }

        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromMinutes(30) };
    }

    public Task<IReadOnlyList<DatasetDto>> GetDatasetsAsync(CancellationToken cancellationToken = default) =>
        GetDatasetsUncheckedAsync(cancellationToken);

    public Task<DatasetSignalOverviewDto?> GetDatasetSignalOverviewAsync(
        Guid datasetId,
        CancellationToken cancellationToken = default) =>
        GetDatasetSignalOverviewUncheckedAsync(datasetId, cancellationToken);

    public Task<DatasetSamplesPageDto?> GetDatasetSamplesPageAsync(
        Guid datasetId,
        int offset = 0,
        int limit = 200,
        CancellationToken cancellationToken = default) =>
        GetDatasetSamplesPageUncheckedAsync(datasetId, offset, limit, cancellationToken);

    public Task<DatasetDto> CreateDatasetAsync(CreateDatasetRequestDto request, CancellationToken cancellationToken = default) =>
        CreateDatasetUncheckedAsync(request, cancellationToken);

    public Task<DatasetDto> GenerateSimulationDatasetAsync(
        GenerateSimulationDatasetRequestDto request,
        CancellationToken cancellationToken = default) =>
        GenerateSimulationUncheckedAsync(request, cancellationToken);

    public Task DeleteDatasetAsync(Guid datasetId, CancellationToken cancellationToken = default) =>
        DeleteDatasetUncheckedAsync(datasetId, cancellationToken);

    public Task<int> ImportCsvSamplesAsync(Guid datasetId, string csvContent, CancellationToken cancellationToken = default) =>
        ImportCsvSamplesUncheckedAsync(datasetId, csvContent, cancellationToken);

    public Task<int> ImportCsvFileMultipartAsync(Guid datasetId, byte[] utf8Csv, string fileName, CancellationToken cancellationToken = default) =>
        ImportCsvFileMultipartUncheckedAsync(datasetId, utf8Csv, fileName, cancellationToken);

    public Task<int> ImportCsvFileAsync(Guid datasetId, string filePath, CancellationToken cancellationToken = default) =>
        ImportCsvFileUncheckedAsync(datasetId, filePath, cancellationToken);

    public Task<ComparisonResultDto> RunComparisonAsync(CreateComparisonRequestDto request, CancellationToken cancellationToken = default) =>
        RunComparisonUncheckedAsync(request, cancellationToken);

    private async Task<IReadOnlyList<DatasetDto>> GetDatasetsUncheckedAsync(CancellationToken cancellationToken)
    {
        var data = await _httpClient.GetFromJsonAsync<List<DatasetDto>>("api/datasets", JsonOptions, cancellationToken);
        return data ?? [];
    }

    private async Task<DatasetSignalOverviewDto?> GetDatasetSignalOverviewUncheckedAsync(
        Guid datasetId,
        CancellationToken cancellationToken) =>
        await _httpClient.GetFromJsonAsync<DatasetSignalOverviewDto>(
            $"api/datasets/{datasetId}/overview",
            JsonOptions,
            cancellationToken);

    private async Task<DatasetSamplesPageDto?> GetDatasetSamplesPageUncheckedAsync(
        Guid datasetId,
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        var uri = $"api/datasets/{datasetId}/samples?offset={offset}&limit={limit}";
        return await _httpClient.GetFromJsonAsync<DatasetSamplesPageDto>(uri, JsonOptions, cancellationToken);
    }

    private async Task<DatasetDto> CreateDatasetUncheckedAsync(CreateDatasetRequestDto request, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync("api/datasets", request, cancellationToken);
        await ThrowUnlessSuccess(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<DatasetDto>(JsonOptions, cancellationToken)
               ?? throw new InvalidOperationException("Create dataset response payload is empty.");
    }

    private async Task<DatasetDto> GenerateSimulationUncheckedAsync(
        GenerateSimulationDatasetRequestDto request,
        CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync("api/simulations/environment", request, cancellationToken);
        await ThrowUnlessSuccess(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<DatasetDto>(JsonOptions, cancellationToken)
               ?? throw new InvalidOperationException("Generate simulation response payload is empty.");
    }

    private async Task DeleteDatasetUncheckedAsync(Guid datasetId, CancellationToken cancellationToken)
    {
        var response = await _httpClient.DeleteAsync($"api/datasets/{datasetId}", cancellationToken);
        await ThrowUnlessSuccess(response, cancellationToken);
    }

    private async Task<int> ImportCsvSamplesUncheckedAsync(Guid datasetId, string csvContent, CancellationToken cancellationToken)
    {
        using var plain = new StringContent(csvContent, Encoding.UTF8, "text/plain");
        var response = await _httpClient.PostAsync($"api/datasets/{datasetId}/samples/import-csv", plain, cancellationToken);
        await ThrowIfImportFailed(response, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, int>>(JsonOptions, cancellationToken);
        return payload is not null && payload.TryGetValue("imported", out var imported) ? imported : 0;
    }

    private async Task<int> ImportCsvFileMultipartUncheckedAsync(
        Guid datasetId,
        byte[] utf8Csv,
        string fileName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(utf8Csv);
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(utf8Csv);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        var safeName = string.IsNullOrWhiteSpace(fileName) ? "import.csv" : Path.GetFileName(fileName.Trim());
        content.Add(fileContent, "file", safeName);
        var response = await _httpClient.PostAsync($"api/datasets/{datasetId}/samples/import-csv-file", content, cancellationToken);
        await ThrowIfImportFailed(response, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, int>>(JsonOptions, cancellationToken);
        return payload is not null && payload.TryGetValue("imported", out var imported) ? imported : 0;
    }

    private async Task ThrowIfImportFailed(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"Import HTTP {(int)response.StatusCode} {response.ReasonPhrase}. {TruncateForMessage(body)}");
    }

    private static string TruncateForMessage(string s, int max = 480) =>
        s.Length <= max ? s : s[..max] + "…";

    private async Task<int> ImportCsvFileUncheckedAsync(Guid datasetId, string filePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("File path is empty.");
        }

        await using var fileStream = File.OpenRead(filePath.Trim());
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", Path.GetFileName(filePath.Trim()));

        var response = await _httpClient.PostAsync($"api/datasets/{datasetId}/samples/import-csv-file", content, cancellationToken);
        await ThrowIfImportFailed(response, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, int>>(JsonOptions, cancellationToken);
        return payload is not null && payload.TryGetValue("imported", out var imported) ? imported : 0;
    }

    private async Task<ComparisonResultDto> RunComparisonUncheckedAsync(
        CreateComparisonRequestDto request,
        CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync("api/comparisons", request, cancellationToken);
        await ThrowUnlessSuccess(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ComparisonResultDto>(JsonOptions, cancellationToken)
               ?? throw new InvalidOperationException("Comparison response payload is empty.");
    }

    private static async Task ThrowUnlessSuccess(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"{(int)response.StatusCode}: {TruncateForMessage(body)}");
    }
}
