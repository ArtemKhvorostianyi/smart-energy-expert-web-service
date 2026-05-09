using ClientServices = SmartEnergyExpert.Client.Services;

namespace SmartEnergyExpert.Client.Apps;

[App(
    icon: Icons.Waves,
    title: "Environment simulation",
    group: ["Datasets"],
    searchHints: ["simulation", "synthetic", "parameter", "temperature", "salinity", "depth", "bottom", "noise", "model"])]
public sealed class EnvironmentSimulationApp : ViewBase
{
    private static readonly string[] SimulationBottomTypes =
        ["sand", "mud", "silt", "clay_mud", "hard_rock", "rock", "granite"];

    public override object? Build()
    {
        var api = UseService<ClientServices.IApiClient>();
        var status = UseState("");
        var busy = UseState(false);

        var simName = UseState("parameter-simulation");
        var depthM = UseState(60m);
        var temperatureC = UseState(12m);
        var salinityPsu = UseState(35m);
        var noiseLevelDb = UseState(-92m);
        var bottomType = UseState("sand");
        var durationMin = UseState(60m);

        var lastDatasetId = UseState<Guid?>(null);

        return Layout.Vertical().Gap(2)
               | Text.H2("Environment-based simulation")
               | Text.P(
                   "Set water column and seabed assumptions; the service builds a heuristic synthetic SPL time series "
                   + "(type simulation, source parameter-synthetic). Use it as the model branch in Hydroacoustic Comparison.")

               | new Card(
                   Layout.Vertical().Gap(1)
                   | Text.H3("Environment")
                   | simName.ToTextInput().Placeholder("Simulation name stem (unique suffix added if needed)")
                   | Text.Muted("Depth (m)")
                   | depthM.ToNumberInput(min: 1, max: 12_000)
                   | Text.Muted("Temperature (°C)")
                   | temperatureC.ToNumberInput(min: -2, max: 40)
                   | Text.Muted("Salinity (PSU)")
                   | salinityPsu.ToNumberInput(min: 0, max: 45)
                   | Text.Muted("Noise floor (dB re 1 µPa, illustrative)")
                   | noiseLevelDb.ToNumberInput(min: -120, max: -20)
                   | Text.Muted("Bottom type")
                   | bottomType.ToSelectInput(SimulationBottomTypes)
                   | Text.Muted("Duration (minutes)")
                   | durationMin.ToNumberInput(min: 1, max: 240)
                   | new Button("Generate simulation dataset").Primary().Disabled(busy.Value).OnClick(async () =>
                   {
                       busy.Set(true);
                       try
                       {
                           var dto = new ClientServices.GenerateSimulationDatasetRequestDto
                           {
                               Name = string.IsNullOrWhiteSpace(simName.Value)
                                   ? "parameter-simulation"
                                   : simName.Value.Trim(),
                               DepthMeters = decimal.Clamp(depthM.Value, 1, 12_000),
                               TemperatureCelsius = decimal.Clamp(temperatureC.Value, -2, 40),
                               SalinityPsu = decimal.Clamp(salinityPsu.Value, 0, 45),
                               NoiseLevelDb = decimal.Clamp(noiseLevelDb.Value, -120, -20),
                               BottomType = string.IsNullOrWhiteSpace(bottomType.Value) ? "sand" : bottomType.Value.Trim(),
                               DurationMinutes = (int)decimal.Round(decimal.Clamp(durationMin.Value, 1, 240))
                           };
                           var ds = await api.GenerateSimulationDatasetAsync(dto);
                           lastDatasetId.Set(ds.Id);
                           status.Set(
                               $"Done: '{ds.Name}' — {ds.SampleCount} samples · {ds.SourceSystem} · id {ds.Id}. "
                               + "Pick it under Simulation in Hydroacoustic Comparison.");
                       }
                       catch (Exception ex)
                       {
                           status.Set($"Generation failed: {ex.Message}");
                       }
                       finally
                       {
                           busy.Set(false);
                       }
                   }))

               | (lastDatasetId.Value is { } gid
                   ? Callout.Info($"Last created dataset id for reference: {gid}")
                   : new Fragment())

               | (string.IsNullOrWhiteSpace(status.Value) ? new Fragment() : Callout.Info(status.Value));
    }
}
