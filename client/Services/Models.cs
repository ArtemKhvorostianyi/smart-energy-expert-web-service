using System.Text.Json.Serialization;

namespace SmartEnergyExpert.Client.Services;

public sealed class DatasetDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("sourceSystem")]
    public string SourceSystem { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("timeRangeStart")]
    public DateTimeOffset TimeRangeStart { get; init; }

    [JsonPropertyName("timeRangeEnd")]
    public DateTimeOffset TimeRangeEnd { get; init; }

    [JsonPropertyName("sampleCount")]
    public int SampleCount { get; init; }
}

public sealed class DatasetSignalOverviewDto
{
    [JsonPropertyName("datasetId")]
    public Guid DatasetId { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("sourceSystem")]
    public string SourceSystem { get; init; } = string.Empty;

    [JsonPropertyName("sampleCount")]
    public int SampleCount { get; init; }

    [JsonPropertyName("durationSeconds")]
    public decimal DurationSeconds { get; init; }

    [JsonPropertyName("frequencyMinHz")]
    public decimal FrequencyMinHz { get; init; }

    [JsonPropertyName("frequencyMaxHz")]
    public decimal FrequencyMaxHz { get; init; }

    [JsonPropertyName("distinctFrequencyBins")]
    public int DistinctFrequencyBins { get; init; }

    [JsonPropertyName("firstTimestamp")]
    public DateTimeOffset FirstTimestamp { get; init; }

    [JsonPropertyName("lastTimestamp")]
    public DateTimeOffset LastTimestamp { get; init; }

    [JsonPropertyName("peakAmplitudeDb")]
    public decimal PeakAmplitudeDb { get; init; }

    [JsonPropertyName("noiseFloorDb")]
    public decimal NoiseFloorDb { get; init; }

    [JsonPropertyName("meanAmplitudeDb")]
    public decimal MeanAmplitudeDb { get; init; }

    [JsonPropertyName("meanNoiseLevelDb")]
    public decimal? MeanNoiseLevelDb { get; init; }
}

public sealed class AcousticSampleRowDto
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("frequencyBand")]
    public decimal FrequencyBand { get; init; }

    [JsonPropertyName("amplitudeDb")]
    public decimal AmplitudeDb { get; init; }

    [JsonPropertyName("depthMeters")]
    public decimal DepthMeters { get; init; }

    [JsonPropertyName("rangeMeters")]
    public decimal RangeMeters { get; init; }

    [JsonPropertyName("soundSpeed")]
    public decimal? SoundSpeed { get; init; }

    [JsonPropertyName("noiseLevelDb")]
    public decimal? NoiseLevelDb { get; init; }
}

public sealed class DatasetSamplesPageDto
{
    [JsonPropertyName("datasetId")]
    public Guid DatasetId { get; init; }

    [JsonPropertyName("datasetName")]
    public string DatasetName { get; init; } = string.Empty;

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; init; }

    [JsonPropertyName("offset")]
    public int Offset { get; init; }

    [JsonPropertyName("limit")]
    public int Limit { get; init; }

    [JsonPropertyName("items")]
    public AcousticSampleRowDto[] Items { get; init; } = [];
}

public sealed class CreateComparisonRequestDto
{
    [JsonPropertyName("simulationDatasetId")]
    public Guid SimulationDatasetId { get; init; }

    [JsonPropertyName("fieldDatasetId")]
    public Guid FieldDatasetId { get; init; }

    [JsonPropertyName("topN")]
    public int TopN { get; init; } = 20;
}

public sealed class CreateDatasetRequestDto
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = "simulation";

    [JsonPropertyName("sourceSystem")]
    public string SourceSystem { get; init; } = "manual";

    [JsonPropertyName("version")]
    public string Version { get; init; } = "v1";
}

public sealed class GenerateSimulationDatasetRequestDto
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "parameter-simulation";

    [JsonPropertyName("depthMeters")]
    public decimal DepthMeters { get; init; } = 60;

    [JsonPropertyName("temperatureCelsius")]
    public decimal TemperatureCelsius { get; init; } = 12;

    [JsonPropertyName("salinityPsu")]
    public decimal SalinityPsu { get; init; } = 35;

    [JsonPropertyName("noiseLevelDb")]
    public decimal NoiseLevelDb { get; init; } = -92;

    [JsonPropertyName("bottomType")]
    public string BottomType { get; init; } = "sand";

    [JsonPropertyName("durationMinutes")]
    public int DurationMinutes { get; init; } = 60;

    [JsonPropertyName("frequencyBandsHz")]
    public decimal[]? FrequencyBandsHz { get; init; }

    [JsonPropertyName("modelVersion")]
    public string? ModelVersion { get; init; }

    /// <summary>POST api/simulations/environment — mirrors every row of this field dataset (timestamps/bands/context).</summary>
    [JsonPropertyName("alignToFieldDatasetId")]
    public Guid? AlignToFieldDatasetId { get; init; }
}

public sealed class DifferencePointDto
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("frequencyBand")]
    public decimal FrequencyBand { get; init; }

    [JsonPropertyName("simulationValue")]
    public decimal SimulationValue { get; init; }

    [JsonPropertyName("fieldValue")]
    public decimal FieldValue { get; init; }

    [JsonPropertyName("absoluteError")]
    public decimal AbsoluteError { get; init; }

    [JsonPropertyName("relativeErrorPercent")]
    public decimal RelativeErrorPercent { get; init; }

    [JsonPropertyName("severity")]
    public string Severity { get; init; } = string.Empty;

    [JsonPropertyName("explanation")]
    public string Explanation { get; init; } = string.Empty;
}

public sealed class RecommendationDto
{
    [JsonPropertyName("reasonCode")]
    public string ReasonCode { get; init; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; init; } = string.Empty;

    [JsonPropertyName("inferenceMethod")]
    public string InferenceMethod { get; init; } = string.Empty;

    [JsonPropertyName("confidenceRationale")]
    public string ConfidenceRationale { get; init; } = string.Empty;

    [JsonPropertyName("evidenceSignals")]
    public string[] EvidenceSignals { get; init; } = [];

    [JsonPropertyName("explanation")]
    public string Explanation { get; init; } = string.Empty;

    [JsonPropertyName("suggestedAction")]
    public string SuggestedAction { get; init; } = string.Empty;

    [JsonPropertyName("confidence")]
    public decimal Confidence { get; init; }
}

public sealed class OverlaySeriesPointDto
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("frequencyBand")]
    public decimal FrequencyBand { get; init; }

    [JsonPropertyName("simulationDb")]
    public decimal SimulationDb { get; init; }

    [JsonPropertyName("fieldDb")]
    public decimal FieldDb { get; init; }
}

public sealed class HeatmapCellDto
{
    [JsonPropertyName("timeBucket")]
    public string TimeBucket { get; init; } = string.Empty;

    [JsonPropertyName("frequencyBand")]
    public decimal FrequencyBand { get; init; }

    [JsonPropertyName("maxRelativeErrorPercent")]
    public decimal MaxRelativeErrorPercent { get; init; }
}

public sealed class DifferenceClusterDto
{
    [JsonPropertyName("ordinal")]
    public int Ordinal { get; init; }

    [JsonPropertyName("timeStart")]
    public DateTimeOffset TimeStart { get; init; }

    [JsonPropertyName("timeEnd")]
    public DateTimeOffset TimeEnd { get; init; }

    [JsonPropertyName("frequencyBand")]
    public decimal FrequencyBand { get; init; }

    [JsonPropertyName("pointCount")]
    public int PointCount { get; init; }

    [JsonPropertyName("meanRelativeErrorPercent")]
    public decimal MeanRelativeErrorPercent { get; init; }
}

public sealed class ComparisonResultDto
{
    /// <summary>Set when pairing used normalized experiment-progress (u in [0,1] per dataset) after exact UTC failed.</summary>
    [JsonPropertyName("timelineNormalizationApplied")]
    public bool TimelineNormalizationApplied { get; init; }

    [JsonPropertyName("comparisonRunId")]
    public Guid ComparisonRunId { get; init; }

    [JsonPropertyName("mae")]
    public decimal Mae { get; init; }

    [JsonPropertyName("rmse")]
    public decimal Rmse { get; init; }

    [JsonPropertyName("meanRelativeErrorPercent")]
    public decimal MeanRelativeErrorPercent { get; init; }

    [JsonPropertyName("p95AbsoluteError")]
    public decimal P95AbsoluteError { get; init; }

    [JsonPropertyName("totalComparedPoints")]
    public int TotalComparedPoints { get; init; }

    [JsonPropertyName("significantDifferenceCount")]
    public int SignificantDifferenceCount { get; init; }

    [JsonPropertyName("topDifferences")]
    public DifferencePointDto[] TopDifferences { get; init; } = [];

    [JsonPropertyName("overlaySeries")]
    public OverlaySeriesPointDto[] OverlaySeries { get; init; } = [];

    [JsonPropertyName("mismatchHeatmap")]
    public HeatmapCellDto[] MismatchHeatmap { get; init; } = [];

    [JsonPropertyName("temporalClusters")]
    public DifferenceClusterDto[] TemporalClusters { get; init; } = [];

    [JsonPropertyName("recommendations")]
    public RecommendationDto[] Recommendations { get; init; } = [];
}
