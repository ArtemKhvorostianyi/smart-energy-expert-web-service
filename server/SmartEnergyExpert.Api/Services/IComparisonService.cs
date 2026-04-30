using SmartEnergyExpert.Api.DTOs;
using SmartEnergyExpert.Api.Entities;

namespace SmartEnergyExpert.Api.Services;

public interface IComparisonService
{
    Task<ComparisonComputationResult> CompareAsync(Dataset simulationDataset, Dataset fieldDataset, int topN, CancellationToken cancellationToken);
}

public sealed class ComparisonComputationResult
{
    public required decimal Mae { get; init; }
    public required decimal Rmse { get; init; }
    public required decimal MeanRelativeErrorPercent { get; init; }
    public required decimal P95AbsoluteError { get; init; }
    public required int TotalComparedPoints { get; init; }
    public required int SignificantDifferenceCount { get; init; }
    public required IReadOnlyList<DifferencePoint> TopDifferences { get; init; }
    public required IReadOnlyList<Recommendation> Recommendations { get; init; }
}
