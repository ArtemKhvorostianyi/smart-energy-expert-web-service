using SmartEnergyExpert.Api.DTOs;
using SmartEnergyExpert.Api.Services;
using Xunit;

namespace SmartEnergyExpert.Api.Tests;

public sealed class ParameterSyntheticHeuristicTests
{
    [Fact]
    public void EstimateAmplitudeDb_at_62_5_kHz_centroid_stays_in_illustrative_SPL_range()
    {
        var sut = new ParameterSyntheticSimulationService();
        var env = new GenerateSimulationDatasetRequest { Name = "range-check", DurationMinutes = 60 };
        var rng = new Random(4242);
        var db = sut.EstimateAmplitudeDb(62500m, surrogateMinuteIndex: 7, env, rng);

        Assert.True(db > -250m, $"Expected SPL above -250 dB illustrative scale, got {db}");
        Assert.True(db < 0m, $"Expected negative illustrative SPL, got {db}");
    }
}
