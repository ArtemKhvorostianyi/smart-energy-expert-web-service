using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartEnergyExpert.Api.Data;
using SmartEnergyExpert.Api.DTOs;
using SmartEnergyExpert.Api.Mapping;

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
        var entities = await dbContext.Recommendations
            .AsNoTracking()
            .Where(x => x.ComparisonRunId == comparisonRunId)
            .OrderByDescending(x => x.Confidence)
            .ToListAsync(cancellationToken);

        return Ok(entities.Select(x => x.ToResponse()).ToList());
    }
}
