using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartEnergyExpert.Api.Data;
using SmartEnergyExpert.Api.DTOs;
using SmartEnergyExpert.Api.Entities;

namespace SmartEnergyExpert.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
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
    [Authorize(Roles = "Admin,Operator")]
    public async Task<ActionResult<Experiment>> Create(
        [FromBody] CreateExperimentRequest request,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("sub");
        var createdBy = Guid.TryParse(userIdClaim, out var parsedUserId) ? parsedUserId : request.CreatedBy;

        if (createdBy == Guid.Empty)
        {
            return BadRequest("Unable to resolve creator user id from token.");
        }

        var experiment = new Experiment
        {
            Title = request.Title.Trim(),
            ExperimentType = request.ExperimentType.Trim(),
            Description = request.Description?.Trim(),
            CreatedBy = createdBy
        };

        dbContext.Experiments.Add(experiment);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetAll), new { id = experiment.Id }, experiment);
    }
}
