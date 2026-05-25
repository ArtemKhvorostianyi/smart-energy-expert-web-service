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
