using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartEnergyExpert.Api.Data;
using SmartEnergyExpert.Api.DTOs;

namespace SmartEnergyExpert.Api.Controllers;

[ApiController]
[Route("api/differences")]
[Authorize]
public sealed class DifferencesController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet("{comparisonRunId:guid}")]
    public async Task<ActionResult<IReadOnlyList<DifferencePointResponse>>> GetTop(
        Guid comparisonRunId,
        [FromQuery] int top = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await dbContext.DifferencePoints
            .AsNoTracking()
            .Where(x => x.ComparisonRunId == comparisonRunId)
            .OrderByDescending(x => x.RelativeErrorPercent)
            .ThenByDescending(x => x.AbsoluteError)
            .Take(Math.Max(1, top))
            .Select(x => new DifferencePointResponse
            {
                Id = x.Id,
                Timestamp = x.Timestamp,
                FrequencyBand = x.FrequencyBand,
                SimulationValue = x.SimulationValue,
                FieldValue = x.FieldValue,
                AbsoluteError = x.AbsoluteError,
                RelativeErrorPercent = x.RelativeErrorPercent,
                Severity = x.Severity,
                Explanation = x.Explanation
            })
            .ToListAsync(cancellationToken);

        return Ok(result);
    }
}
