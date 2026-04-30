using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
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

    [HttpPost("{datasetId:guid}/samples/import-csv")]
    [Authorize(Roles = "Admin,Expert")]
    [Consumes("text/plain")]
    public async Task<ActionResult<object>> ImportCsv(Guid datasetId, [FromBody] string csvContent, CancellationToken cancellationToken)
    {
        var dataset = await dbContext.Datasets.FirstOrDefaultAsync(x => x.Id == datasetId, cancellationToken);
        if (dataset is null)
        {
            return NotFound("Dataset not found.");
        }

        if (string.IsNullOrWhiteSpace(csvContent))
        {
            return BadRequest("CSV content is empty.");
        }

        var imported = 0;
        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("timestamp", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var cells = line.Split(',', StringSplitOptions.TrimEntries);
            if (cells.Length < 7)
            {
                continue;
            }

            if (!DateTimeOffset.TryParse(cells[0], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp))
            {
                continue;
            }

            if (!decimal.TryParse(cells[1], CultureInfo.InvariantCulture, out var frequencyBand) ||
                !decimal.TryParse(cells[2], CultureInfo.InvariantCulture, out var amplitudeDb) ||
                !decimal.TryParse(cells[3], CultureInfo.InvariantCulture, out var depthMeters) ||
                !decimal.TryParse(cells[4], CultureInfo.InvariantCulture, out var rangeMeters))
            {
                continue;
            }

            decimal? soundSpeed = decimal.TryParse(cells[5], CultureInfo.InvariantCulture, out var speed) ? speed : null;
            decimal? noiseLevel = decimal.TryParse(cells[6], CultureInfo.InvariantCulture, out var noise) ? noise : null;

            dbContext.AcousticSamples.Add(new AcousticSample
            {
                DatasetId = dataset.Id,
                Timestamp = timestamp,
                FrequencyBand = frequencyBand,
                AmplitudeDb = amplitudeDb,
                DepthMeters = depthMeters,
                RangeMeters = rangeMeters,
                SoundSpeed = soundSpeed,
                NoiseLevelDb = noiseLevel
            });

            if (dataset.TimeRangeStart == default || timestamp < dataset.TimeRangeStart)
            {
                dataset.TimeRangeStart = timestamp;
            }

            if (dataset.TimeRangeEnd == default || timestamp > dataset.TimeRangeEnd)
            {
                dataset.TimeRangeEnd = timestamp;
            }

            imported++;
        }

        dataset.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new { imported });
    }
}
