using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        var (score, riskLevel, recommendationText) = evaluationService.Calculate(experiment);

        var evaluation = new Evaluation
        {
            ExperimentId = experimentId,
            ExpertId = expertId,
            IntegralScore = score,
            RiskLevel = riskLevel,
            Conclusion = request.Conclusion
        };

        var recommendation = new Recommendation
        {
            Evaluation = evaluation,
            DecisionText = recommendationText,
            Priority = riskLevel switch
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
            IntegralScore = score,
            RiskLevel = riskLevel,
            Recommendation = recommendationText
        });
    }
}
