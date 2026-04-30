using System.Text.Json.Serialization;

namespace SmartEnergyExpert.Client.Services;

public sealed class LoginRequestDto
{
    [JsonPropertyName("email")]
    public string Email { get; init; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; init; } = string.Empty;
}

public sealed class LoginResponseDto
{
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; init; } = string.Empty;

    [JsonPropertyName("expiresInSeconds")]
    public int ExpiresInSeconds { get; init; }
}

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

public sealed class CreateComparisonRequestDto
{
    [JsonPropertyName("simulationDatasetId")]
    public Guid SimulationDatasetId { get; init; }

    [JsonPropertyName("fieldDatasetId")]
    public Guid FieldDatasetId { get; init; }

    [JsonPropertyName("topN")]
    public int TopN { get; init; } = 20;
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

    [JsonPropertyName("explanation")]
    public string Explanation { get; init; } = string.Empty;

    [JsonPropertyName("suggestedAction")]
    public string SuggestedAction { get; init; } = string.Empty;

    [JsonPropertyName("confidence")]
    public decimal Confidence { get; init; }
}

public sealed class ComparisonResultDto
{
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

    [JsonPropertyName("recommendations")]
    public RecommendationDto[] Recommendations { get; init; } = [];
}
