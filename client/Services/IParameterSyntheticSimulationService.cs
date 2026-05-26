using SmartEnergyExpert.Client.Data;
using SmartEnergyExpert.Client.DTOs;
using SmartEnergyExpert.Client.Entities;

namespace SmartEnergyExpert.Client.Services;

public interface IParameterSyntheticSimulationService
{
    Task<(Dataset Dataset, int SampleCount)> GenerateAndPersistAsync(
        AppDbContext dbContext,
        GenerateSimulationDatasetRequest request,
        Guid? ownerUserId,
        bool isSharedCatalog = false,
        CancellationToken cancellationToken = default);

    /// <summary>RNG-shaped heuristic SPL (same as environment generator) at a surrogate “minute” index.</summary>
    decimal EstimateAmplitudeDb(
        decimal frequencyBandHz,
        int surrogateMinuteIndex,
        GenerateSimulationDatasetRequest envelope,
        Random rng);
}
