namespace SmartEnergyExpert.Api.DTOs;

public sealed class DatasetResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string SourceSystem { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public DateTimeOffset TimeRangeStart { get; init; }
    public DateTimeOffset TimeRangeEnd { get; init; }
    public int SampleCount { get; init; }
}

/// <summary>Aggregates derived from acoustic samples for signal explorer / DSS inspect step.</summary>
public sealed class DatasetSignalOverviewResponse
{
    public Guid DatasetId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string SourceSystem { get; init; } = string.Empty;
    public int SampleCount { get; init; }
    public decimal DurationSeconds { get; init; }
    public decimal FrequencyMinHz { get; init; }
    public decimal FrequencyMaxHz { get; init; }
    public int DistinctFrequencyBins { get; init; }
    public DateTimeOffset FirstTimestamp { get; init; }
    public DateTimeOffset LastTimestamp { get; init; }
    public decimal PeakAmplitudeDb { get; init; }
    /// <summary>Lower tail estimate (≈10th percentile of amplitude).</summary>
    public decimal NoiseFloorDb { get; init; }
    public decimal MeanAmplitudeDb { get; init; }
    public decimal? MeanNoiseLevelDb { get; init; }
}

public sealed class CreateDatasetRequest
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string SourceSystem { get; init; } = string.Empty;
    public string Version { get; init; } = "v1";
}

public sealed class AddAcousticSampleRequest
{
    public DateTimeOffset Timestamp { get; init; }
    public decimal FrequencyBand { get; init; }
    public decimal AmplitudeDb { get; init; }
    public decimal DepthMeters { get; init; }
    public decimal RangeMeters { get; init; }
    public decimal? SoundSpeed { get; init; }
    public decimal? NoiseLevelDb { get; init; }
}

public sealed class CreateComparisonRequest
{
    public Guid SimulationDatasetId { get; init; }
    public Guid FieldDatasetId { get; init; }
    public int TopN { get; init; } = 20;
}

public sealed class DifferencePointResponse
{
    public Guid Id { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public decimal FrequencyBand { get; init; }
    public decimal SimulationValue { get; init; }
    public decimal FieldValue { get; init; }
    public decimal AbsoluteError { get; init; }
    public decimal RelativeErrorPercent { get; init; }
    public string Severity { get; init; } = string.Empty;
    public string Explanation { get; init; } = string.Empty;
}

public sealed class RecommendationResponse
{
    public string ReasonCode { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string InferenceMethod { get; init; } = string.Empty;
    public string ConfidenceRationale { get; init; } = string.Empty;
    public IReadOnlyList<string> EvidenceSignals { get; init; } = [];
    public string Explanation { get; init; } = string.Empty;
    public string SuggestedAction { get; init; } = string.Empty;
    public decimal Confidence { get; init; }
}

public sealed class OverlaySeriesPointResponse
{
    public DateTimeOffset Timestamp { get; init; }
    public decimal FrequencyBand { get; init; }
    public decimal SimulationDb { get; init; }
    public decimal FieldDb { get; init; }
}

public sealed class HeatmapCellResponse
{
    public string TimeBucket { get; init; } = string.Empty;
    public decimal FrequencyBand { get; init; }
    public decimal MaxRelativeErrorPercent { get; init; }
}

public sealed class DifferenceClusterResponse
{
    public int Ordinal { get; init; }
    public DateTimeOffset TimeStart { get; init; }
    public DateTimeOffset TimeEnd { get; init; }
    public decimal FrequencyBand { get; init; }
    public int PointCount { get; init; }
    public decimal MeanRelativeErrorPercent { get; init; }
}

public sealed class ComparisonResultResponse
{
    public Guid ComparisonRunId { get; init; }
    public decimal Mae { get; init; }
    public decimal Rmse { get; init; }
    public decimal MeanRelativeErrorPercent { get; init; }
    public decimal P95AbsoluteError { get; init; }
    public int TotalComparedPoints { get; init; }
    public int SignificantDifferenceCount { get; init; }
    public IReadOnlyList<DifferencePointResponse> TopDifferences { get; init; } = [];
    public IReadOnlyList<OverlaySeriesPointResponse> OverlaySeries { get; init; } = [];
    public IReadOnlyList<HeatmapCellResponse> MismatchHeatmap { get; init; } = [];
    public IReadOnlyList<DifferenceClusterResponse> TemporalClusters { get; init; } = [];
    public IReadOnlyList<RecommendationResponse> Recommendations { get; init; } = [];
}
