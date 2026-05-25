using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartEnergyExpert.Client.Data;
using SmartEnergyExpert.Client.DTOs;
using SmartEnergyExpert.Client.Entities;
using SmartEnergyExpert.Client.Mapping;

namespace SmartEnergyExpert.Client.Services;

/// <summary>Direct PostgreSQL access for Ivy apps (replaces HTTP ApiClient).</summary>
public sealed class HydroacousticService(
    IDbContextFactory<AppDbContext> dbFactory,
    IComparisonService comparisonService,
    IParameterSyntheticSimulationService simulationService) : IApiClient
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<DatasetDto>> GetDatasetsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var result = await db.Datasets
            .AsNoTracking()
            .Select(x => new DatasetDto
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
        return result;
    }

    public async Task<DatasetSignalOverviewDto?> GetDatasetSignalOverviewAsync(
        Guid datasetId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var dataset = await db.Datasets.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == datasetId, cancellationToken);
        if (dataset is null)
        {
            return null;
        }

        var samplesQuery = db.AcousticSamples.AsNoTracking().Where(x => x.DatasetId == datasetId);
        var count = await samplesQuery.CountAsync(cancellationToken);
        if (count == 0)
        {
            return new DatasetSignalOverviewDto
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
            };
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

        return new DatasetSignalOverviewDto
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
        };
    }

    public async Task<DatasetSamplesPageDto?> GetDatasetSamplesPageAsync(
        Guid datasetId,
        int offset = 0,
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var dataset = await db.Datasets.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == datasetId, cancellationToken);
        if (dataset is null)
        {
            return null;
        }

        var take = Math.Clamp(limit, 1, 2_000);
        var skip = Math.Max(0, offset);

        var total = await db.AcousticSamples.AsNoTracking()
            .CountAsync(x => x.DatasetId == datasetId, cancellationToken);

        var items = await db.AcousticSamples.AsNoTracking()
            .Where(x => x.DatasetId == datasetId)
            .OrderBy(x => x.Timestamp).ThenBy(x => x.FrequencyBand)
            .Skip(skip)
            .Take(take)
            .Select(x => new AcousticSampleRowDto
            {
                Timestamp = x.Timestamp,
                FrequencyBand = x.FrequencyBand,
                AmplitudeDb = x.AmplitudeDb,
                DepthMeters = x.DepthMeters,
                RangeMeters = x.RangeMeters,
                SoundSpeed = x.SoundSpeed,
                NoiseLevelDb = x.NoiseLevelDb
            })
            .ToListAsync(cancellationToken);

        return new DatasetSamplesPageDto
        {
            DatasetId = dataset.Id,
            DatasetName = dataset.Name,
            TotalCount = total,
            Offset = skip,
            Limit = take,
            Items = items.ToArray()
        };
    }

    public async Task<DatasetDto> CreateDatasetAsync(
        CreateDatasetRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Type))
        {
            throw new InvalidOperationException("Dataset name and type are required.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var dataset = new Dataset
        {
            Name = request.Name.Trim(),
            Type = request.Type.Trim().ToLowerInvariant(),
            SourceSystem = string.IsNullOrWhiteSpace(request.SourceSystem) ? "unknown" : request.SourceSystem.Trim(),
            Version = request.Version.Trim()
        };

        db.Datasets.Add(dataset);
        await db.SaveChangesAsync(cancellationToken);

        return new DatasetDto
        {
            Id = dataset.Id,
            Name = dataset.Name,
            Type = dataset.Type,
            SourceSystem = dataset.SourceSystem,
            Version = dataset.Version,
            TimeRangeStart = dataset.TimeRangeStart,
            TimeRangeEnd = dataset.TimeRangeEnd,
            SampleCount = 0
        };
    }

    public async Task<DatasetDto> GenerateSimulationDatasetAsync(
        GenerateSimulationDatasetRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (request.AlignToFieldDatasetId is { } fieldDatasetId)
        {
            var fieldDataset = await db.Datasets.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == fieldDatasetId, cancellationToken);
            if (fieldDataset is null)
            {
                throw new InvalidOperationException($"Field dataset {fieldDatasetId} was not found.");
            }

            if (!string.Equals(fieldDataset.Type, "field", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("alignToFieldDatasetId must reference a dataset with type \"field\".");
            }

            var sampleCount =
                await db.AcousticSamples.CountAsync(x => x.DatasetId == fieldDatasetId, cancellationToken);
            if (sampleCount == 0)
            {
                throw new InvalidOperationException("Align target dataset has no acoustic samples.");
            }
        }

        var (dataset, sampleCountReturned) = await simulationService.GenerateAndPersistAsync(
            db,
            ToGenerateRequest(request),
            cancellationToken);

        return new DatasetDto
        {
            Id = dataset.Id,
            Name = dataset.Name,
            Type = dataset.Type,
            SourceSystem = dataset.SourceSystem,
            Version = dataset.Version,
            TimeRangeStart = dataset.TimeRangeStart,
            TimeRangeEnd = dataset.TimeRangeEnd,
            SampleCount = sampleCountReturned
        };
    }

    public async Task DeleteDatasetAsync(Guid datasetId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var dataset = await db.Datasets.FirstOrDefaultAsync(x => x.Id == datasetId, cancellationToken);
        if (dataset is null)
        {
            throw new InvalidOperationException("Dataset not found.");
        }

        var linkedRuns = await db.ComparisonRuns
            .Where(r => r.SimulationDatasetId == datasetId || r.FieldDatasetId == datasetId)
            .ToListAsync(cancellationToken);
        db.ComparisonRuns.RemoveRange(linkedRuns);
        db.Datasets.Remove(dataset);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<int> ImportCsvSamplesAsync(
        Guid datasetId,
        string csvContent,
        CancellationToken cancellationToken = default) =>
        ImportCsvCoreAsync(datasetId, csvContent, cancellationToken);

    public Task<int> ImportCsvFileMultipartAsync(
        Guid datasetId,
        byte[] utf8Csv,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(utf8Csv);
        var csv = Encoding.UTF8.GetString(utf8Csv);
        return ImportCsvCoreAsync(datasetId, csv, cancellationToken);
    }

    public async Task<int> ImportCsvFileAsync(
        Guid datasetId,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("File path is empty.");
        }

        var csv = await File.ReadAllTextAsync(filePath.Trim(), cancellationToken);
        return await ImportCsvCoreAsync(datasetId, csv, cancellationToken);
    }

    public async Task<ComparisonResultDto> RunComparisonAsync(
        CreateComparisonRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var simulationDataset = await db.Datasets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.SimulationDatasetId && x.Type == "simulation", cancellationToken);
        var fieldDataset = await db.Datasets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.FieldDatasetId && x.Type == "field", cancellationToken);

        if (simulationDataset is null || fieldDataset is null)
        {
            throw new InvalidOperationException(
                "Both simulation and field datasets must exist and have proper types.");
        }

        var computed = await comparisonService.CompareAsync(
            simulationDataset,
            fieldDataset,
            request.TopN,
            cancellationToken);

        var run = new ComparisonRun
        {
            SimulationDatasetId = simulationDataset.Id,
            FieldDatasetId = fieldDataset.Id,
            Status = "completed",
            Mae = computed.Mae,
            Rmse = computed.Rmse,
            MeanRelativeErrorPercent = computed.MeanRelativeErrorPercent,
            P95AbsoluteError = computed.P95AbsoluteError,
            TotalComparedPoints = computed.TotalComparedPoints,
            SignificantDifferenceCount = computed.SignificantDifferenceCount,
            VisualizationPayloadJson = SerializeVisualization(computed.Visualization)
        };

        foreach (var point in computed.TopDifferences)
        {
            point.ComparisonRunId = run.Id;
            run.Differences.Add(point);
        }

        foreach (var recommendation in computed.Recommendations)
        {
            recommendation.ComparisonRunId = run.Id;
            run.Recommendations.Add(recommendation);
        }

        db.ComparisonRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);

        return MapComparisonResult(run, computed.Visualization.TimelineNormalizationApplied);
    }

    private async Task<int> ImportCsvCoreAsync(
        Guid datasetId,
        string csvContent,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            throw new InvalidOperationException("CSV content is empty.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var dataset = await db.Datasets.FirstOrDefaultAsync(x => x.Id == datasetId, cancellationToken);
        if (dataset is null)
        {
            throw new InvalidOperationException("Dataset not found.");
        }

        return await AcousticCsvBatchImporter.ImportIntoDatasetAsync(db, dataset, csvContent, cancellationToken);
    }

    private static GenerateSimulationDatasetRequest ToGenerateRequest(GenerateSimulationDatasetRequestDto dto) =>
        new()
        {
            Name = dto.Name,
            DepthMeters = dto.DepthMeters,
            TemperatureCelsius = dto.TemperatureCelsius,
            SalinityPsu = dto.SalinityPsu,
            NoiseLevelDb = dto.NoiseLevelDb,
            BottomType = dto.BottomType,
            DurationMinutes = dto.DurationMinutes,
            FrequencyBandsHz = dto.FrequencyBandsHz,
            ModelVersion = dto.ModelVersion,
            AlignToFieldDatasetId = dto.AlignToFieldDatasetId
        };

    private static string SerializeVisualization(ComparisonVisualizationComputation visualization)
    {
        var snapshot = new VisualizationSnapshotDto
        {
            TimelineNormalizationApplied = visualization.TimelineNormalizationApplied,
            DominantVisualizationFrequencyBand = visualization.DominantVisualizationFrequencyBand,
            OverlaySeries = visualization.OverlaySeries
                .Select(x => new OverlaySeriesPointResponse
                {
                    Timestamp = x.Timestamp,
                    FrequencyBand = x.FrequencyBand,
                    SimulationDb = x.SimulationDb,
                    FieldDb = x.FieldDb
                })
                .ToList(),
            MismatchHeatmap = visualization.HeatmapCells
                .Select(x => new HeatmapCellResponse
                {
                    TimeBucket = x.TimeBucket,
                    FrequencyBand = x.FrequencyBand,
                    MaxRelativeErrorPercent = x.MaxRelativeErrorPercent
                })
                .ToList(),
            TemporalClusters = visualization.TemporalClusters
                .Select(x => new DifferenceClusterResponse
                {
                    Ordinal = x.Ordinal,
                    TimeStart = x.TimeStart,
                    TimeEnd = x.TimeEnd,
                    FrequencyBand = x.FrequencyBand,
                    PointCount = x.PointCount,
                    MeanRelativeErrorPercent = x.MeanRelativeErrorPercent
                })
                .ToList()
        };

        return JsonSerializer.Serialize(snapshot, WebJson);
    }

    private static ComparisonResultDto MapComparisonResult(ComparisonRun run, bool timelineNormalizationApplied)
    {
        var viz = TryDeserializeVisualization(run.VisualizationPayloadJson);
        return new ComparisonResultDto
        {
            TimelineNormalizationApplied = timelineNormalizationApplied || (viz?.TimelineNormalizationApplied ?? false),
            ComparisonRunId = run.Id,
            Mae = run.Mae,
            Rmse = run.Rmse,
            MeanRelativeErrorPercent = run.MeanRelativeErrorPercent,
            P95AbsoluteError = run.P95AbsoluteError,
            TotalComparedPoints = run.TotalComparedPoints,
            SignificantDifferenceCount = run.SignificantDifferenceCount,
            TopDifferences = run.Differences
                .OrderByDescending(x => x.RelativeErrorPercent)
                .Select(x => new DifferencePointDto
                {
                    Timestamp = x.Timestamp,
                    FrequencyBand = x.FrequencyBand,
                    SimulationValue = x.SimulationValue,
                    FieldValue = x.FieldValue,
                    AbsoluteError = x.AbsoluteError,
                    RelativeErrorPercent = x.RelativeErrorPercent,
                    Severity = x.Severity,
                    Explanation = x.Explanation
                })
                .ToArray(),
            OverlaySeries = viz?.OverlaySeries.Select(ToOverlayDto).ToArray() ?? [],
            MismatchHeatmap = viz?.MismatchHeatmap.Select(ToHeatmapDto).ToArray() ?? [],
            TemporalClusters = viz?.TemporalClusters.Select(ToClusterDto).ToArray() ?? [],
            Recommendations = run.Recommendations
                .OrderByDescending(x => x.Confidence)
                .Select(x => ToRecommendationDto(x.ToResponse()))
                .ToArray()
        };
    }

    private static OverlaySeriesPointDto ToOverlayDto(OverlaySeriesPointResponse x) =>
        new()
        {
            Timestamp = x.Timestamp,
            FrequencyBand = x.FrequencyBand,
            SimulationDb = x.SimulationDb,
            FieldDb = x.FieldDb
        };

    private static HeatmapCellDto ToHeatmapDto(HeatmapCellResponse x) =>
        new()
        {
            TimeBucket = x.TimeBucket,
            FrequencyBand = x.FrequencyBand,
            MaxRelativeErrorPercent = x.MaxRelativeErrorPercent
        };

    private static DifferenceClusterDto ToClusterDto(DifferenceClusterResponse x) =>
        new()
        {
            Ordinal = x.Ordinal,
            TimeStart = x.TimeStart,
            TimeEnd = x.TimeEnd,
            FrequencyBand = x.FrequencyBand,
            PointCount = x.PointCount,
            MeanRelativeErrorPercent = x.MeanRelativeErrorPercent
        };

    private static RecommendationDto ToRecommendationDto(RecommendationResponse x) =>
        new()
        {
            ReasonCode = x.ReasonCode,
            Category = x.Category,
            InferenceMethod = x.InferenceMethod,
            ConfidenceRationale = x.ConfidenceRationale,
            EvidenceSignals = x.EvidenceSignals.ToArray(),
            Explanation = x.Explanation,
            SuggestedAction = x.SuggestedAction,
            Confidence = x.Confidence
        };

    private static VisualizationSnapshotDto? TryDeserializeVisualization(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<VisualizationSnapshotDto>(json, WebJson);
        }
        catch
        {
            return null;
        }
    }

    private sealed class VisualizationSnapshotDto
    {
        public bool TimelineNormalizationApplied { get; set; }
        public decimal DominantVisualizationFrequencyBand { get; set; }
        public List<OverlaySeriesPointResponse> OverlaySeries { get; set; } = [];
        public List<HeatmapCellResponse> MismatchHeatmap { get; set; } = [];
        public List<DifferenceClusterResponse> TemporalClusters { get; set; } = [];
    }
}
