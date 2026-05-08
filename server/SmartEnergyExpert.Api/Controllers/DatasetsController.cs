using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using SmartEnergyExpert.Api.Data;
using SmartEnergyExpert.Api.DTOs;
using SmartEnergyExpert.Api.Entities;
using SmartEnergyExpert.Api.Services;

namespace SmartEnergyExpert.Api.Controllers;

[ApiController]
[Route("api/datasets")]
[Authorize]
public sealed class DatasetsController(
    AppDbContext dbContext,
    IParameterSyntheticSimulationService parameterSyntheticSimulation) : ControllerBase
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

    [HttpPost("generate-simulation")]
    [Authorize(Roles = "Admin,Expert")]
    public async Task<ActionResult<DatasetResponse>> GenerateParameterSimulation(
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

    [HttpGet("{datasetId:guid}/overview")]
    public async Task<ActionResult<DatasetSignalOverviewResponse>> GetSignalOverview(Guid datasetId, CancellationToken cancellationToken)
    {
        var dataset = await dbContext.Datasets.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == datasetId, cancellationToken);
        if (dataset is null)
        {
            return NotFound("Dataset not found.");
        }

        var samplesQuery = dbContext.AcousticSamples.AsNoTracking().Where(x => x.DatasetId == datasetId);
        var count = await samplesQuery.CountAsync(cancellationToken);
        if (count == 0)
        {
            return Ok(new DatasetSignalOverviewResponse
            {
                DatasetId = dataset.Id,
                Name = dataset.Name,
                Type = dataset.Type,
                SourceSystem = dataset.SourceSystem,
                SampleCount = 0,
                FrequencyMinHz = 0,
                FrequencyMaxHz = 0,
                DistinctFrequencyBins = 0,
                FirstTimestamp = default,
                LastTimestamp = default
            });
        }

        var distinctFreq = await samplesQuery.Select(x => x.FrequencyBand).Distinct().CountAsync(cancellationToken);
        var agg = await samplesQuery
            .GroupBy(_ => 1)
            .Select(g => new
            {
                MinTs = g.Min(x => x.Timestamp),
                MaxTs = g.Max(x => x.Timestamp),
                Fmin = g.Min(x => x.FrequencyBand),
                Fmax = g.Max(x => x.FrequencyBand),
                Peak = g.Max(x => x.AmplitudeDb),
                MeanAmp = g.Average(x => x.AmplitudeDb)
            })
            .FirstAsync(cancellationToken);

        var p10Index = (int)Math.Clamp(Math.Floor((count - 1) * 0.1m), 0, count - 1);
        var noiseFloorDb = await samplesQuery.OrderBy(x => x.AmplitudeDb)
            .Skip(p10Index)
            .Select(x => x.AmplitudeDb)
            .FirstAsync(cancellationToken);

        decimal? meanNoiseLevel = await samplesQuery.AnyAsync(x => x.NoiseLevelDb != null, cancellationToken)
            ? await samplesQuery.Where(x => x.NoiseLevelDb != null).AverageAsync(x => x.NoiseLevelDb!.Value, cancellationToken)
            : null;

        var durationSeconds =
            agg.MaxTs == agg.MinTs
                ? 0m
                : decimal.Round((decimal)(agg.MaxTs - agg.MinTs).TotalSeconds, 4);

        return Ok(new DatasetSignalOverviewResponse
        {
            DatasetId = dataset.Id,
            Name = dataset.Name,
            Type = dataset.Type,
            SourceSystem = dataset.SourceSystem,
            SampleCount = count,
            DurationSeconds = durationSeconds,
            FrequencyMinHz = agg.Fmin,
            FrequencyMaxHz = agg.Fmax,
            DistinctFrequencyBins = distinctFreq,
            FirstTimestamp = agg.MinTs,
            LastTimestamp = agg.MaxTs,
            PeakAmplitudeDb = decimal.Round(agg.Peak, 4),
            NoiseFloorDb = decimal.Round(noiseFloorDb, 4),
            MeanAmplitudeDb = decimal.Round(agg.MeanAmp, 4),
            MeanNoiseLevelDb = meanNoiseLevel is null ? null : decimal.Round(meanNoiseLevel.Value, 4)
        });
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

    [HttpPost("{datasetId:guid}/samples/import-csv-file")]
    [Authorize(Roles = "Admin,Expert")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<object>> ImportCsvFile(Guid datasetId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("CSV file is empty.");
        }

        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        var csvContent = await reader.ReadToEndAsync(cancellationToken);
        return await ImportCsv(datasetId, csvContent, cancellationToken);
    }
}
