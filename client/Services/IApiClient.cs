using SmartEnergyExpert.Client.Services.Auth;

namespace SmartEnergyExpert.Client.Services;

public interface IApiClient
{
    Task<IReadOnlyList<DatasetDto>> GetDatasetsAsync(
        DatasetAccessContext access,
        CancellationToken cancellationToken = default);

    Task<DatasetSignalOverviewDto?> GetDatasetSignalOverviewAsync(
        Guid datasetId,
        DatasetAccessContext access,
        CancellationToken cancellationToken = default);

    Task<DatasetSamplesPageDto?> GetDatasetSamplesPageAsync(
        Guid datasetId,
        DatasetAccessContext access,
        int offset = 0,
        int limit = 200,
        CancellationToken cancellationToken = default);

    Task<DatasetDto> CreateDatasetAsync(
        CreateDatasetRequestDto request,
        DatasetAccessContext access,
        CancellationToken cancellationToken = default);

    Task<DatasetDto> GenerateSimulationDatasetAsync(
        GenerateSimulationDatasetRequestDto request,
        DatasetAccessContext access,
        CancellationToken cancellationToken = default);

    Task DeleteDatasetAsync(
        Guid datasetId,
        DatasetAccessContext access,
        CancellationToken cancellationToken = default);

    Task<int> ImportCsvSamplesAsync(
        Guid datasetId,
        string csvContent,
        DatasetAccessContext access,
        CancellationToken cancellationToken = default);

    Task<int> ImportCsvFileMultipartAsync(
        Guid datasetId,
        byte[] utf8Csv,
        string fileName,
        DatasetAccessContext access,
        CancellationToken cancellationToken = default);

    Task<int> ImportCsvFileAsync(
        Guid datasetId,
        string filePath,
        DatasetAccessContext access,
        CancellationToken cancellationToken = default);

    Task<ComparisonResultDto> RunComparisonAsync(
        CreateComparisonRequestDto request,
        DatasetAccessContext access,
        CancellationToken cancellationToken = default);
}
