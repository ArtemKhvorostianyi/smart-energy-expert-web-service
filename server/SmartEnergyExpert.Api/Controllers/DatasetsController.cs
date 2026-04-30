using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartEnergyExpert.Api.Data;
using SmartEnergyExpert.Api.DTOs;
using SmartEnergyExpert.Api.Entities;

namespace SmartEnergyExpert.Api.Controllers;

[ApiController]
[Route("api/datasets")]
[Authorize]
public sealed class DatasetsController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DatasetResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await dbContext.Datasets
            .AsNoTracking()
            .Select(x => new DatasetResponse
            {
                Id = x.Id,
                Name = x.Name,
                Type = x.Type,
                SourceSystem = x.SourceSystem,
                Version = x.Version,
                TimeRangeStart = x.TimeRangeStart,
                TimeRangeEnd = x.TimeRangeEnd,
                SampleCount = x.Samples.Count
            })
            .OrderByDescending(x => x.TimeRangeStart)
            .ToListAsync(cancellationToken);

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Expert")]
    public async Task<ActionResult<DatasetResponse>> Create([FromBody] CreateDatasetRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Type))
        {
            return BadRequest("Dataset name and type are required.");
        }

        var dataset = new Dataset
        {
            Name = request.Name.Trim(),
            Type = request.Type.Trim().ToLowerInvariant(),
            SourceSystem = string.IsNullOrWhiteSpace(request.SourceSystem) ? "unknown" : request.SourceSystem.Trim(),
            Version = request.Version.Trim()
        };

        dbContext.Datasets.Add(dataset);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new DatasetResponse
        {
            Id = dataset.Id,
            Name = dataset.Name,
            Type = dataset.Type,
            SourceSystem = dataset.SourceSystem,
            Version = dataset.Version,
            TimeRangeStart = dataset.TimeRangeStart,
            TimeRangeEnd = dataset.TimeRangeEnd,
            SampleCount = 0
        });
    }

    [HttpPost("{datasetId:guid}/samples")]
    [Authorize(Roles = "Admin,Expert")]
    public async Task<ActionResult> AddSample(Guid datasetId, [FromBody] AddAcousticSampleRequest request, CancellationToken cancellationToken)
    {
        var dataset = await dbContext.Datasets.FirstOrDefaultAsync(x => x.Id == datasetId, cancellationToken);
        if (dataset is null)
        {
            return NotFound("Dataset not found.");
        }

        dbContext.AcousticSamples.Add(new AcousticSample
        {
            DatasetId = dataset.Id,
            Timestamp = request.Timestamp,
            FrequencyBand = request.FrequencyBand,
            AmplitudeDb = request.AmplitudeDb,
            DepthMeters = request.DepthMeters,
            RangeMeters = request.RangeMeters,
            SoundSpeed = request.SoundSpeed,
            NoiseLevelDb = request.NoiseLevelDb
        });

        if (dataset.TimeRangeStart == default || request.Timestamp < dataset.TimeRangeStart)
        {
            dataset.TimeRangeStart = request.Timestamp;
        }

        if (dataset.TimeRangeEnd == default || request.Timestamp > dataset.TimeRangeEnd)
        {
            dataset.TimeRangeEnd = request.Timestamp;
        }

        dataset.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
