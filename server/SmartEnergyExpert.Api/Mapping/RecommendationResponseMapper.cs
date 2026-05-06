using System.Text.Json;
using SmartEnergyExpert.Api.DTOs;
using SmartEnergyExpert.Api.Entities;

namespace SmartEnergyExpert.Api.Mapping;

internal static class RecommendationResponseMapper
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    internal static RecommendationResponse ToResponse(this Recommendation recommendation) =>
        new()
        {
            ReasonCode = recommendation.ReasonCode,
            Category = recommendation.Category,
            InferenceMethod = recommendation.InferenceMethod,
            ConfidenceRationale = recommendation.ConfidenceRationale,
            EvidenceSignals = DeserializeEvidence(recommendation.EvidenceSignalsJson),
            Explanation = recommendation.Explanation,
            SuggestedAction = recommendation.SuggestedAction,
            Confidence = recommendation.Confidence
        };

    private static IReadOnlyList<string> DeserializeEvidence(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, WebJson) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
