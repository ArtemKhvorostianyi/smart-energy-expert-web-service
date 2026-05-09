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
    public ComparisonVisualizationComputation Visualization { get; init; } = new();
}

public sealed class ComparisonVisualizationComputation
{
    /// <summary>
    /// True when pairs used **experiment-progress** alignment (0→1 normalized time independently per dataset) after exact UTC and UTC-window nearest failed — works without shared wall-clock epochs.
    /// </summary>
    public bool TimelineNormalizationApplied { get; init; }

    public decimal DominantVisualizationFrequencyBand { get; init; }
    public IReadOnlyList<OverlaySeriesComputationPoint> OverlaySeries { get; init; } = [];
    public IReadOnlyList<HeatmapComputationCell> HeatmapCells { get; init; } = [];
    public IReadOnlyList<TemporalDifferenceClusterComputation> TemporalClusters { get; init; } = [];
}

public sealed class OverlaySeriesComputationPoint
{
    public DateTimeOffset Timestamp { get; init; }
    public decimal FrequencyBand { get; init; }
    public decimal SimulationDb { get; init; }
    public decimal FieldDb { get; init; }
}

public sealed class HeatmapComputationCell
{
    public string TimeBucket { get; init; } = string.Empty;
    public decimal FrequencyBand { get; init; }
    public decimal MaxRelativeErrorPercent { get; init; }
}

public sealed record TemporalDifferenceClusterComputation(
    int Ordinal,
    DateTimeOffset TimeStart,
    DateTimeOffset TimeEnd,
    decimal FrequencyBand,
    int PointCount,
    decimal MeanRelativeErrorPercent);
