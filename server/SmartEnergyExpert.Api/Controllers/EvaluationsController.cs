using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using SmartEnergyExpert.Api.Data;
using SmartEnergyExpert.Api.DTOs;
using SmartEnergyExpert.Api.Entities;
using SmartEnergyExpert.Api.Services;

namespace SmartEnergyExpert.Api.Controllers;

[ApiController]
[Route("api/experiments/{experimentId:guid}/evaluation")]
[Authorize]
public sealed class EvaluationsController(AppDbContext dbContext, IEvaluationService evaluationService) : ControllerBase
{
    [HttpGet("latest")]
    public async Task<ActionResult<EvaluationResultResponse>> GetLatest(
        [FromRoute] Guid experimentId,
        CancellationToken cancellationToken)
    {
        var latestEvaluation = await dbContext.Evaluations
            .AsNoTracking()
            .Where(x => x.ExperimentId == experimentId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                Evaluation = x,
                Recommendation = x.Recommendation != null ? x.Recommendation.DecisionText : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (latestEvaluation is null)
        {
            return NotFound($"No evaluations found for experiment '{experimentId}'.");
        }

        var topFactors = string.IsNullOrWhiteSpace(latestEvaluation.Evaluation.TopFactors)
            ? []
            : JsonSerializer.Deserialize<string[]>(latestEvaluation.Evaluation.TopFactors!) ?? [];

        return Ok(new EvaluationResultResponse
        {
            EvaluationId = latestEvaluation.Evaluation.Id,
            IntegralScore = latestEvaluation.Evaluation.IntegralScore,
            RiskLevel = latestEvaluation.Evaluation.RiskLevel,
            Recommendation = latestEvaluation.Recommendation ?? string.Empty,
            Conclusion = latestEvaluation.Evaluation.Conclusion,
            Explanation = latestEvaluation.Evaluation.Explanation ?? string.Empty,
            TopFactors = topFactors,
            Status = latestEvaluation.Evaluation.Status,
            EvaluatedAt = latestEvaluation.Evaluation.CreatedAt
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Expert")]
    public async Task<ActionResult<EvaluationResultResponse>> Evaluate(
        [FromRoute] Guid experimentId,
        [FromBody] EvaluateExperimentRequest request,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(userIdClaim, out var expertId))
        {
            return Unauthorized("Unable to resolve expert id from token.");
        }

        var experiment = await dbContext.Experiments
            .Include(x => x.Parameters)
            .FirstOrDefaultAsync(x => x.Id == experimentId, cancellationToken);

        if (experiment is null)
        {
            return NotFound($"Experiment '{experimentId}' was not found.");
        }

        var activeCriteria = await dbContext.Criteria
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);

        var criteriaIds = activeCriteria.Select(x => x.Id).ToArray();

        var configuredWeights = await dbContext.CriterionWeights
            .AsNoTracking()
            .Where(x => x.IsActive
                && criteriaIds.Contains(x.CriterionId)
                && (x.ExperimentType == experiment.ExperimentType || x.ExperimentType == "default"))
            .OrderByDescending(x => x.ExperimentType == experiment.ExperimentType)
            .ToListAsync(cancellationToken);

        var weightMap = configuredWeights
            .GroupBy(x => x.CriterionId)
            .ToDictionary(x => x.Key, x => x.First().Weight);

        var result = evaluationService.Calculate(experiment, activeCriteria, weightMap);

        var evaluation = new Evaluation
        {
            ExperimentId = experimentId,
            ExpertId = expertId,
            IntegralScore = result.Score,
            RiskLevel = result.RiskLevel,
            Conclusion = request.Conclusion,
            Explanation = result.Explanation,
            TopFactors = JsonSerializer.Serialize(result.TopFactors),
            Status = result.Status
        };

        var recommendation = new Recommendation
        {
            Evaluation = evaluation,
            DecisionText = result.Recommendation,
            Priority = result.RiskLevel switch
            {
                "critical" => 1,
                "high" => 2,
                "moderate" => 3,
                _ => 4
            }
        };

        experiment.Status = "evaluated";
        experiment.UpdatedAt = DateTimeOffset.UtcNow;

        dbContext.Evaluations.Add(evaluation);
        dbContext.Recommendations.Add(recommendation);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new EvaluationResultResponse
        {
            EvaluationId = evaluation.Id,
            IntegralScore = result.Score,
            RiskLevel = result.RiskLevel,
            Recommendation = result.Recommendation,
            Conclusion = evaluation.Conclusion,
            Explanation = result.Explanation,
            TopFactors = result.TopFactors.ToArray(),
            Status = result.Status,
            EvaluatedAt = evaluation.CreatedAt
        });
    }
}
