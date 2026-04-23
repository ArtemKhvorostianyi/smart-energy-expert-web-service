using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartEnergyExpert.Api.Data;
using SmartEnergyExpert.Api.DTOs;
using SmartEnergyExpert.Api.Entities;

namespace SmartEnergyExpert.Api.Controllers;

[ApiController]
[Route("api/experiments/{experimentId:guid}/parameters")]
[Authorize]
public sealed class ExperimentParametersController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ExperimentParameterResponse>>> GetByExperiment(
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
            .Select(x => new ExperimentParameterResponse
            {
                Id = x.Id,
                ExperimentId = x.ExperimentId,
                ParameterName = x.ParameterName,
                Value = x.Value,
                Unit = x.Unit,
                MinAcceptable = x.MinAcceptable,
                MaxAcceptable = x.MaxAcceptable,
                Weight = x.Weight,
                Category = x.Category,
                Description = x.Description,
                IsCritical = x.IsCritical,
                Source = x.Source,
                MeasuredAt = x.MeasuredAt,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(parameters);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Operator")]
    public async Task<ActionResult<ExperimentParameterResponse>> Add(
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

        var criterion = await dbContext.Criteria
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Name == request.ParameterName.Trim() && x.IsActive, cancellationToken);

        decimal? criterionWeight = null;
        if (criterion is not null)
        {
            criterionWeight = await dbContext.CriterionWeights
                .AsNoTracking()
                .Where(x => x.CriterionId == criterion.Id
                    && x.IsActive
                    && (x.ExperimentType == experiment.ExperimentType || x.ExperimentType == "default"))
                .OrderByDescending(x => x.ExperimentType == experiment.ExperimentType)
                .Select(x => (decimal?)x.Weight)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var parameter = new ExperimentParameter
        {
            ExperimentId = experimentId,
            ParameterName = request.ParameterName.Trim(),
            Value = request.Value,
            Unit = request.Unit.Trim(),
            MinAcceptable = request.MinAcceptable ?? criterion?.MinValue,
            MaxAcceptable = request.MaxAcceptable ?? criterion?.MaxValue,
            Weight = request.Weight ?? criterionWeight ?? criterion?.DefaultWeight,
            Category = string.IsNullOrWhiteSpace(request.Category) ? "physical" : request.Category.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? criterion?.Description : request.Description.Trim(),
            IsCritical = request.IsCritical,
            Source = string.IsNullOrWhiteSpace(request.Source) ? "manual" : request.Source.Trim(),
            MeasuredAt = request.MeasuredAt
        };

        experiment.UpdatedAt = DateTimeOffset.UtcNow;
        dbContext.ExperimentParameters.Add(parameter);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new ExperimentParameterResponse
        {
            Id = parameter.Id,
            ExperimentId = parameter.ExperimentId,
            ParameterName = parameter.ParameterName,
            Value = parameter.Value,
            Unit = parameter.Unit,
            MinAcceptable = parameter.MinAcceptable,
            MaxAcceptable = parameter.MaxAcceptable,
            Weight = parameter.Weight,
            Category = parameter.Category,
            Description = parameter.Description,
            IsCritical = parameter.IsCritical,
            Source = parameter.Source,
            MeasuredAt = parameter.MeasuredAt,
            CreatedAt = parameter.CreatedAt
        };

        return CreatedAtAction(nameof(GetByExperiment), new { experimentId }, response);
    }

    [HttpDelete("{parameterId:guid}")]
    [Authorize(Roles = "Admin,Operator")]
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
