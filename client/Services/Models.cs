using System.Text.Json.Serialization;

namespace SmartEnergyExpert.Client.Services;

public sealed class UserSession
{
    public string AccessToken { get; init; } = string.Empty;
    public int ExpiresInSeconds { get; init; }
    public Guid UserId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
}

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

    [JsonPropertyName("userId")]
    public Guid UserId { get; init; }

    [JsonPropertyName("fullName")]
    public string FullName { get; init; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; init; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;
}

public sealed class ExperimentDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("experimentType")]
    public string ExperimentType { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class CreateExperimentRequestDto
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("experimentType")]
    public string ExperimentType { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("createdBy")]
    public Guid CreatedBy { get; init; }
}

public sealed class AddParameterRequestDto
{
    [JsonPropertyName("parameterName")]
    public string ParameterName { get; init; } = string.Empty;

    [JsonPropertyName("value")]
    public decimal Value { get; init; }

    [JsonPropertyName("unit")]
    public string Unit { get; init; } = string.Empty;

    [JsonPropertyName("measuredAt")]
    public DateTimeOffset? MeasuredAt { get; init; }
}

public sealed class EvaluationRequestDto
{
    [JsonPropertyName("conclusion")]
    public string? Conclusion { get; init; }
}

public sealed class EvaluationResultDto
{
    [JsonPropertyName("evaluationId")]
    public Guid EvaluationId { get; init; }

    [JsonPropertyName("integralScore")]
    public decimal IntegralScore { get; init; }

    [JsonPropertyName("riskLevel")]
    public string RiskLevel { get; init; } = string.Empty;

    [JsonPropertyName("recommendation")]
    public string Recommendation { get; init; } = string.Empty;
}

