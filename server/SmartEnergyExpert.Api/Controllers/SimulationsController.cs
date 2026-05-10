using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartEnergyExpert.Api.Data;
using SmartEnergyExpert.Api.DTOs;
using SmartEnergyExpert.Api.Services;

namespace SmartEnergyExpert.Api.Controllers;

/// <summary>Endpoints that synthesize datasets (not CRUD under <c>/api/datasets</c>).</summary>
[ApiController]
[Route("api/simulations")]
public sealed class SimulationsController(
    AppDbContext dbContext,
    IParameterSyntheticSimulationService parameterSyntheticSimulation) : ControllerBase
{
    /// <summary>Builds and persists a parameterized simulation dataset (tabular samples).</summary>
    [HttpPost("environment")]
    public async Task<ActionResult<DatasetResponse>> GenerateFromEnvironmentParameters(
        [FromBody] GenerateSimulationDatasetRequest request,
        CancellationToken cancellationToken)
    {
        if (request.AlignToFieldDatasetId is { } fieldDatasetId)
        {
            var fieldDataset = await dbContext.Datasets.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == fieldDatasetId, cancellationToken);
            if (fieldDataset is null)
            {
                return NotFound($"Field dataset {fieldDatasetId} was not found.");
            }

            if (!string.Equals(fieldDataset.Type, "field", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("alignToFieldDatasetId must reference a dataset with type \"field\".");
            }

            var sampleCount =
                await dbContext.AcousticSamples.CountAsync(x => x.DatasetId == fieldDatasetId, cancellationToken);
            if (sampleCount == 0)
            {
                return BadRequest("Align target dataset has no acoustic samples.");
            }
        }

        var (dataset, sampleCountReturned) =
            await parameterSyntheticSimulation.GenerateAndPersistAsync(dbContext, request, cancellationToken);

        return Ok(new DatasetResponse
        {
            Id = dataset.Id,
            Name = dataset.Name,
            Type = dataset.Type,
            SourceSystem = dataset.SourceSystem,
            Version = dataset.Version,
            TimeRangeStart = dataset.TimeRangeStart,
            TimeRangeEnd = dataset.TimeRangeEnd,
            SampleCount = sampleCountReturned
        });
    }
}
