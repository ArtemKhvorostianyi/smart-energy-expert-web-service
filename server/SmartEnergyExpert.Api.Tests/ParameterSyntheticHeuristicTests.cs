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

    /// <summary>Глибина входить у евристику: більша глибина дає інший (нижчий) рівень при тому ж RNG.</summary>
    [Fact]
    public void EstimateAmplitudeDb_deeper_water_changes_level_vs_shallow()
    {
        var sut = new ParameterSyntheticSimulationService();
        var rng = new Random(777);
        const decimal band = 800m;
        const int minute = 3;

        var shallow = new GenerateSimulationDatasetRequest
        {
            Name = "shallow",
            DurationMinutes = 60,
            DepthMeters = 25m,
            TemperatureCelsius = 12m,
            SalinityPsu = 35m,
            NoiseLevelDb = -92m,
            BottomType = "sand"
        };
        var deep = new GenerateSimulationDatasetRequest
        {
            Name = "deep",
            DurationMinutes = 60,
            DepthMeters = 500m,
            TemperatureCelsius = 12m,
            SalinityPsu = 35m,
            NoiseLevelDb = -92m,
            BottomType = "sand"
        };

        var a = sut.EstimateAmplitudeDb(band, minute, shallow, rng);
        var b = sut.EstimateAmplitudeDb(band, minute, deep, new Random(777));

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void EstimateAmplitudeDb_same_rng_seed_produces_identical_value()
    {
        var sut = new ParameterSyntheticSimulationService();
        var env = new GenerateSimulationDatasetRequest
        {
            Name = "seeded",
            DurationMinutes = 30,
            DepthMeters = 60m,
            TemperatureCelsius = 12m,
            SalinityPsu = 35m,
            NoiseLevelDb = -90m,
            BottomType = "mud"
        };
        var rng1 = new Random(202601);
        var rng2 = new Random(202601);

        var a = sut.EstimateAmplitudeDb(1200m, 10, env, rng1);
        var b = sut.EstimateAmplitudeDb(1200m, 10, env, rng2);

        Assert.Equal(a, b);
    }
}
