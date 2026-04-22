using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartEnergyExpert.Api.Data;
using SmartEnergyExpert.Api.DTOs;
using SmartEnergyExpert.Api.Entities;

namespace SmartEnergyExpert.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ExperimentsController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<Experiment>>> GetAll(CancellationToken cancellationToken)
    {
        var experiments = await dbContext.Experiments
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(experiments);
    }

    [HttpPost]
    public async Task<ActionResult<Experiment>> Create(
        [FromBody] CreateExperimentRequest request,
        CancellationToken cancellationToken)
    {
        var experiment = new Experiment
        {
            Title = request.Title.Trim(),
            ExperimentType = request.ExperimentType.Trim(),
            Description = request.Description?.Trim(),
            CreatedBy = request.CreatedBy
        };

        dbContext.Experiments.Add(experiment);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetAll), new { id = experiment.Id }, experiment);
    }
}
