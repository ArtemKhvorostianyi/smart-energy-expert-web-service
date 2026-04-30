using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartEnergyExpert.Api.Data;
using SmartEnergyExpert.Api.DTOs;

namespace SmartEnergyExpert.Api.Controllers;

[ApiController]
[Route("api/recommendations")]
[Authorize]
public sealed class RecommendationsController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet("{comparisonRunId:guid}")]
    public async Task<ActionResult<IReadOnlyList<RecommendationResponse>>> Get(
        Guid comparisonRunId,
        CancellationToken cancellationToken = default)
    {
        var result = await dbContext.Recommendations
            .AsNoTracking()
            .Where(x => x.ComparisonRunId == comparisonRunId)
            .OrderByDescending(x => x.Confidence)
            .Select(x => new RecommendationResponse
            {
                ReasonCode = x.ReasonCode,
                Explanation = x.Explanation,
                SuggestedAction = x.SuggestedAction,
                Confidence = x.Confidence
            })
            .ToListAsync(cancellationToken);

        return Ok(result);
    }
}
