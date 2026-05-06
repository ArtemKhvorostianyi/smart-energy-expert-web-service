using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartEnergyExpert.Api.Data;
using SmartEnergyExpert.Api.DTOs;
using SmartEnergyExpert.Api.Entities;
using SmartEnergyExpert.Api.Mapping;
using SmartEnergyExpert.Api.Services;

namespace SmartEnergyExpert.Api.Controllers;

[ApiController]
[Route("api/comparisons")]
[Authorize]
public sealed class ComparisonsController(AppDbContext dbContext, IComparisonService comparisonService) : ControllerBase
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [HttpPost]
    [Authorize(Roles = "Admin,Expert")]
    public async Task<ActionResult<ComparisonResultResponse>> Run([FromBody] CreateComparisonRequest request, CancellationToken cancellationToken)
    {
        var simulationDataset = await dbContext.Datasets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.SimulationDatasetId && x.Type == "simulation", cancellationToken);
        var fieldDataset = await dbContext.Datasets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.FieldDatasetId && x.Type == "field", cancellationToken);

        if (simulationDataset is null || fieldDataset is null)
        {
            return BadRequest("Both simulation and field datasets must exist and have proper types.");
        }

        var computed = await comparisonService.CompareAsync(simulationDataset, fieldDataset, request.TopN, cancellationToken);
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

        dbContext.ComparisonRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(MapResult(run));
    }

    [HttpGet("{comparisonRunId:guid}")]
    public async Task<ActionResult<ComparisonResultResponse>> Get(Guid comparisonRunId, CancellationToken cancellationToken)
    {
        var run = await dbContext.ComparisonRuns
            .AsNoTracking()
            .Include(x => x.Differences)
            .Include(x => x.Recommendations)
            .FirstOrDefaultAsync(x => x.Id == comparisonRunId, cancellationToken);

        if (run is null)
        {
            return NotFound("Comparison run not found.");
        }

        return Ok(MapResult(run));
    }

    private static string SerializeVisualization(ComparisonVisualizationComputation visualization)
    {
        var snapshot = new VisualizationSnapshotDto
        {
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

    private static ComparisonResultResponse MapResult(ComparisonRun run)
    {
        var viz = TryDeserializeVisualization(run.VisualizationPayloadJson);
        return new ComparisonResultResponse
        {
            ComparisonRunId = run.Id,
            Mae = run.Mae,
            Rmse = run.Rmse,
            MeanRelativeErrorPercent = run.MeanRelativeErrorPercent,
            P95AbsoluteError = run.P95AbsoluteError,
            TotalComparedPoints = run.TotalComparedPoints,
            SignificantDifferenceCount = run.SignificantDifferenceCount,
            TopDifferences = run.Differences
                .OrderByDescending(x => x.RelativeErrorPercent)
                .Select(x => new DifferencePointResponse
                {
                    Id = x.Id,
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
            OverlaySeries = viz?.OverlaySeries ?? [],
            MismatchHeatmap = viz?.MismatchHeatmap ?? [],
            TemporalClusters = viz?.TemporalClusters ?? [],
            Recommendations = run.Recommendations
                .OrderByDescending(x => x.Confidence)
                .Select(x => x.ToResponse())
                .ToArray()
        };
    }

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
        public decimal DominantVisualizationFrequencyBand { get; set; }
        public List<OverlaySeriesPointResponse> OverlaySeries { get; set; } = [];
        public List<HeatmapCellResponse> MismatchHeatmap { get; set; } = [];
        public List<DifferenceClusterResponse> TemporalClusters { get; set; } = [];
    }
}
