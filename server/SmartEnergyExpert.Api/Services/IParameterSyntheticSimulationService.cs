using SmartEnergyExpert.Api.Data;
using SmartEnergyExpert.Api.DTOs;
using SmartEnergyExpert.Api.Entities;

namespace SmartEnergyExpert.Api.Services;

public interface IParameterSyntheticSimulationService
{
    /// <summary>Creates a simulation-type dataset persisted with synthesized acoustic samples.</summary>
    Task<(Dataset Dataset, int SampleCount)> GenerateAndPersistAsync(
        AppDbContext dbContext,
        GenerateSimulationDatasetRequest request,
        CancellationToken cancellationToken);
}
