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
public sealed class CriteriaController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<Criterion>>> GetAll(CancellationToken cancellationToken)
    {
        var criteria = await dbContext.Criteria
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Ok(criteria);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Expert")]
    public async Task<ActionResult<Criterion>> Create([FromBody] UpsertCriterionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.MinValue >= request.MaxValue || request.DefaultWeight <= 0)
        {
            return BadRequest("Invalid criterion payload.");
        }

        var criterion = new Criterion
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            MinValue = request.MinValue,
            MaxValue = request.MaxValue,
            DefaultWeight = request.DefaultWeight,
            IsActive = request.IsActive
        };

        dbContext.Criteria.Add(criterion);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id = criterion.Id }, criterion);
    }

    [HttpPut("{criterionId:guid}")]
    [Authorize(Roles = "Admin,Expert")]
    public async Task<ActionResult<Criterion>> Update(
        [FromRoute] Guid criterionId,
        [FromBody] UpsertCriterionRequest request,
        CancellationToken cancellationToken)
    {
        var criterion = await dbContext.Criteria.FirstOrDefaultAsync(x => x.Id == criterionId, cancellationToken);
        if (criterion is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.MinValue >= request.MaxValue || request.DefaultWeight <= 0)
        {
            return BadRequest("Invalid criterion payload.");
        }

        criterion.Name = request.Name.Trim();
        criterion.Description = request.Description?.Trim();
        criterion.MinValue = request.MinValue;
        criterion.MaxValue = request.MaxValue;
        criterion.DefaultWeight = request.DefaultWeight;
        criterion.IsActive = request.IsActive;
        criterion.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(criterion);
    }

    [HttpPut("{criterionId:guid}/weights")]
    [Authorize(Roles = "Admin,Expert")]
    public async Task<ActionResult<CriterionWeight>> UpsertWeight(
        [FromRoute] Guid criterionId,
        [FromBody] UpsertCriterionWeightRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ExperimentType) || request.Weight <= 0)
        {
            return BadRequest("Invalid weight payload.");
        }

        var criterionExists = await dbContext.Criteria.AnyAsync(x => x.Id == criterionId, cancellationToken);
        if (!criterionExists)
        {
            return NotFound($"Criterion '{criterionId}' was not found.");
        }

        var experimentType = request.ExperimentType.Trim();
        var weight = await dbContext.CriterionWeights.FirstOrDefaultAsync(
            x => x.CriterionId == criterionId && x.ExperimentType == experimentType,
            cancellationToken);

        if (weight is null)
        {
            weight = new CriterionWeight
            {
                CriterionId = criterionId,
                ExperimentType = experimentType,
                Weight = request.Weight,
                IsActive = request.IsActive
            };

            dbContext.CriterionWeights.Add(weight);
        }
        else
        {
            weight.Weight = request.Weight;
            weight.IsActive = request.IsActive;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(weight);
    }
}
