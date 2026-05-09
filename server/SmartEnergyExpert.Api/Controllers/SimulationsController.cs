using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartEnergyExpert.Api.Data;
using SmartEnergyExpert.Api.DTOs;
using SmartEnergyExpert.Api.Services;

namespace SmartEnergyExpert.Api.Controllers;

/// <summary>Endpoints that synthesize datasets (not CRUD under <c>/api/datasets</c>).</summary>
[ApiController]
[Route("api/simulations")]
[Authorize]
public sealed class SimulationsController(
    AppDbContext dbContext,
    IParameterSyntheticSimulationService parameterSyntheticSimulation) : ControllerBase
{
    /// <summary>Builds and persists a parameterized simulation dataset (tabular samples).</summary>
    [HttpPost("environment")]
    [Authorize(Roles = "Admin,Expert")]
    public async Task<ActionResult<DatasetResponse>> GenerateFromEnvironmentParameters(
        [FromBody] GenerateSimulationDatasetRequest request,
        CancellationToken cancellationToken)
    {
        var (dataset, sampleCount) =
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
            SampleCount = sampleCount
        });
    }
}
