using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartEnergyExpert.Api.Data;
using SmartEnergyExpert.Api.DTOs;
using SmartEnergyExpert.Api.Entities;
using SmartEnergyExpert.Api.Services;

namespace SmartEnergyExpert.Api.Controllers;

[ApiController]
[Route("api/comparisons")]
[Authorize]
public sealed class ComparisonsController(AppDbContext dbContext, IComparisonService comparisonService) : ControllerBase
{
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
            SignificantDifferenceCount = computed.SignificantDifferenceCount
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

    private static ComparisonResultResponse MapResult(ComparisonRun run) =>
        new()
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
            Recommendations = run.Recommendations
                .OrderByDescending(x => x.Confidence)
                .Select(x => new RecommendationResponse
                {
                    ReasonCode = x.ReasonCode,
                    Explanation = x.Explanation,
                    SuggestedAction = x.SuggestedAction,
                    Confidence = x.Confidence
                })
                .ToArray()
        };
}
