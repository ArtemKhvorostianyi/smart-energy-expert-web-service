using SmartEnergyExpert.Api.Data;
using SmartEnergyExpert.Api.DTOs;
using SmartEnergyExpert.Api.Entities;

namespace SmartEnergyExpert.Api.Services;

public interface IParameterSyntheticSimulationService
{
    Task<(Dataset Dataset, int SampleCount)> GenerateAndPersistAsync(
        AppDbContext dbContext,
        GenerateSimulationDatasetRequest request,
        CancellationToken cancellationToken);

    /// <summary>RNG-shaped heuristic SPL (same as environment generator) at a surrogate “minute” index.</summary>
    decimal EstimateAmplitudeDb(
        decimal frequencyBandHz,
        int surrogateMinuteIndex,
        GenerateSimulationDatasetRequest envelope,
        Random rng);
}
