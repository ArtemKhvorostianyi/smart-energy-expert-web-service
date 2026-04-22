using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartEnergyExpert.Api.Data;
using SmartEnergyExpert.Api.DTOs;
using SmartEnergyExpert.Api.Entities;

namespace SmartEnergyExpert.Api.Controllers;

[ApiController]
[Route("api/experiments/{experimentId:guid}/parameters")]
public sealed class ExperimentParametersController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ExperimentParameter>>> GetByExperiment(
        [FromRoute] Guid experimentId,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.Experiments.AnyAsync(x => x.Id == experimentId, cancellationToken);
        if (!exists)
        {
            return NotFound($"Experiment '{experimentId}' was not found.");
        }

        var parameters = await dbContext.ExperimentParameters
            .AsNoTracking()
            .Where(x => x.ExperimentId == experimentId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(parameters);
    }

    [HttpPost]
    public async Task<ActionResult<ExperimentParameter>> Add(
        [FromRoute] Guid experimentId,
        [FromBody] AddExperimentParameterRequest request,
        CancellationToken cancellationToken)
    {
        var experiment = await dbContext.Experiments.FirstOrDefaultAsync(x => x.Id == experimentId, cancellationToken);
        if (experiment is null)
        {
            return NotFound($"Experiment '{experimentId}' was not found.");
        }

        if (string.IsNullOrWhiteSpace(request.ParameterName) || string.IsNullOrWhiteSpace(request.Unit))
        {
            return BadRequest("Parameter name and unit are required.");
        }

        var parameter = new ExperimentParameter
        {
            ExperimentId = experimentId,
            ParameterName = request.ParameterName.Trim(),
            Value = request.Value,
            Unit = request.Unit.Trim(),
            MeasuredAt = request.MeasuredAt
        };

        experiment.UpdatedAt = DateTimeOffset.UtcNow;
        dbContext.ExperimentParameters.Add(parameter);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetByExperiment), new { experimentId }, parameter);
    }

    [HttpDelete("{parameterId:guid}")]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid experimentId,
        [FromRoute] Guid parameterId,
        CancellationToken cancellationToken)
    {
        var parameter = await dbContext.ExperimentParameters
            .FirstOrDefaultAsync(x => x.Id == parameterId && x.ExperimentId == experimentId, cancellationToken);

        if (parameter is null)
        {
            return NotFound($"Parameter '{parameterId}' was not found in experiment '{experimentId}'.");
        }

        dbContext.ExperimentParameters.Remove(parameter);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
