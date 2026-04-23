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
[Authorize(Roles = "Admin,Expert")]
public sealed class EvaluationsController(AppDbContext dbContext, IEvaluationService evaluationService) : ControllerBase
{
    [HttpPost]
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
            Explanation = result.Explanation,
            TopFactors = result.TopFactors.ToArray(),
            Status = result.Status
        });
    }
}
