namespace SmartEnergyExpert.Client.Apps;

[App(icon: Icons.FlaskConical, title: "Experiments", searchHints: ["experiments", "parameters", "input", "measurements"])]
public sealed class ExperimentsApp : ViewBase
{
    private static readonly string[] ExperimentTypeOptions =
    [
        "Thermal Stability Test",
        "Power Load Test",
        "Communication Reliability Test",
        "Sensor Accuracy Test",
        "Emergency Mode Test",
        "Equipment Degradation Assessment"
    ];

    private static readonly ParameterPreset[] ParameterPresets =
    [
        new("temperature", "Temperature", "C", ["C", "F", "K"], 22m, 10m, 25m, 0.20m, "physical", "sensor", false),
        new("pressure", "Pressure", "kPa", ["kPa", "bar", "atm", "Pa"], 101.3m, 95m, 110m, 0.18m, "physical", "sensor", false),
        new("voltage", "Voltage", "V", ["V", "kV"], 230m, 210m, 240m, 0.20m, "electrical", "sensor", true),
        new("current", "Current", "A", ["A", "mA"], 8m, 0m, 12m, 0.18m, "electrical", "sensor", true),
        new("power", "Power", "kW", ["W", "kW", "MW"], 5m, 1m, 8m, 0.17m, "electrical", "sensor", false),
        new("frequency", "Frequency", "Hz", ["Hz", "kHz"], 50m, 49m, 51m, 0.15m, "electrical", "sensor", false),
        new("vibration", "Vibration", "mm/s", ["mm/s", "m/s"], 2.5m, 0m, 4.5m, 0.14m, "reliability", "sensor", false),
        new("rotation_speed", "Rotation Speed", "rpm", ["rpm", "rps"], 1500m, 1200m, 1800m, 0.12m, "physical", "sensor", false),
        new("humidity", "Humidity", "%", ["%"], 45m, 30m, 70m, 0.10m, "physical", "sensor", false),
        new("coolant_level", "Coolant Level", "%", ["%"], 75m, 40m, 100m, 0.16m, "physical", "sensor", true),
        new("operating_hours", "Operating Hours", "h", ["h", "days"], 1200m, 0m, 20000m, 0.08m, "reliability", "api", false),
        new("failure_count", "Failure Count", "count", ["count"], 0m, 0m, 3m, 0.16m, "reliability", "api", true),
        new("last_maintenance_days", "Last Maintenance Days", "days", ["days", "h"], 20m, 0m, 90m, 0.11m, "reliability", "api", false),
        new("wear_level", "Wear Level", "%", ["%"], 15m, 0m, 60m, 0.13m, "reliability", "api", false),
        new("sensor_health", "Sensor Health", "%", ["%"], 95m, 70m, 100m, 0.14m, "reliability", "api", true),
        new("component_load_percent", "Component Load Percent", "%", ["%"], 60m, 20m, 90m, 0.12m, "reliability", "api", false),
        new("packet_loss", "Packet Loss", "%", ["%"], 0.2m, 0m, 2m, 0.18m, "network", "api", true),
        new("latency_ms", "Latency", "ms", ["ms", "s"], 20m, 0m, 100m, 0.16m, "network", "api", false),
        new("unauthorized_access_attempts", "Unauthorized Access Attempts", "count", ["count"], 0m, 0m, 1m, 0.20m, "network", "api", true),
        new("communication_status", "Communication Status", "score", ["score"], 1m, 0m, 1m, 0.14m, "network", "api", true),
        new("data_integrity_score", "Data Integrity Score", "%", ["%"], 99m, 90m, 100m, 0.17m, "network", "api", true),
        new("system_response_time", "System Response Time", "ms", ["ms", "s"], 120m, 0m, 600m, 0.13m, "network", "api", false),
        new("expert_confidence", "Expert Confidence", "%", ["%"], 85m, 50m, 100m, 0.09m, "expert", "manual", false),
        new("anomaly_level", "Anomaly Level", "score", ["score"], 0.2m, 0m, 1m, 0.15m, "expert", "manual", true),
        new("risk_probability", "Risk Probability", "%", ["%"], 12m, 0m, 100m, 0.18m, "expert", "manual", true),
        new("impact_level", "Impact Level", "score", ["score"], 2m, 1m, 5m, 0.16m, "expert", "manual", true),
        new("decision_priority", "Decision Priority", "score", ["score"], 3m, 1m, 5m, 0.14m, "expert", "manual", false)
    ];

    private static readonly string[] ParameterNameOptions = ParameterPresets.Select(x => x.Label).ToArray();
    private static readonly string[] CategoryOptions = ["physical", "reliability", "network", "expert", "context", "electrical"];
    private static readonly string[] SourceOptions = ["manual", "sensor", "api", "import"];

    public override object? Build()
    {
        var bladesView = UseBlades(() => new ExperimentComposerBlade(), "Experiment Designer");
        return bladesView;
    }

    private sealed class ExperimentComposerBlade : ViewBase
    {
        public override object? Build()
        {
            var apiClient = UseService<SmartEnergyExpert.Client.Services.IApiClient>();
            var navigator = UseNavigation();
            var title = UseState("");
            var experimentType = UseState(ExperimentTypeOptions[0]);
            var description = UseState("");
            var parameters = UseState(new List<ParameterDraft>());
            var statusMessage = UseState("");
            var parameterName = UseState(ParameterNameOptions[0]);
            var parameterValue = UseState(ParameterPresets[0].DefaultValue);
            var parameterUnit = UseState(ParameterPresets[0].DefaultUnit);
            var parameterCategory = UseState(ParameterPresets[0].DefaultCategory);
            var parameterSource = UseState(SourceOptions[1]);
            var manualMetadataOverride = UseState(false);
            var parameterDescription = UseState("");
            var parameterMin = UseState(ParameterPresets[0].DefaultMin);
            var parameterMax = UseState(ParameterPresets[0].DefaultMax);
            var parameterWeight = UseState(ParameterPresets[0].DefaultWeight);
            var useDefaultThresholds = UseState(true);
            var isCritical = UseState(ParameterPresets[0].DefaultCritical);
            var previousUnit = UseState(ParameterPresets[0].DefaultUnit);
            var blades = UseContext<IBladeContext>();
            var (sheetView, showSheet) = UseTrigger((IState<bool> isOpen) =>
                isOpen.Value
                    ? new Sheet(_ => isOpen.Set(false),
                        Layout.Vertical()
                        | parameterName.ToSelectInput(ParameterNameOptions)
                        | parameterValue.ToNumberInput().Placeholder("Value")
                        | parameterUnit.ToSelectInput(ResolvePreset(parameterName.Value).Units)
                        | parameterDescription.ToTextInput().Placeholder("Description (optional)")
                        | manualMetadataOverride.ToBoolInput().Label("Override category/source manually")
                        | (manualMetadataOverride.Value
                            ? Layout.Vertical()
                                | parameterCategory.ToSelectInput(CategoryOptions)
                                | parameterSource.ToSelectInput(SourceOptions)
                            : Text.Muted($"Category: {parameterCategory.Value} | Source: {parameterSource.Value} (auto from template)"))
                        | isCritical.ToBoolInput().Label("Critical parameter")
                        | useDefaultThresholds.ToBoolInput().Label("Use criteria defaults for min/max/weight")
                        | (useDefaultThresholds.Value
                            ? Text.Muted("Defaults will be taken from criteria and criterion weights.")
                            : Layout.Vertical()
                                | parameterMin.ToNumberInput().Placeholder("Min acceptable")
                                | parameterMax.ToNumberInput().Placeholder("Max acceptable")
                                | parameterWeight.ToNumberInput(min: 0.01, max: 1.0).Placeholder("Weight (0..1)"))
                        | new Button("Add to scenario")
                            .Primary()
                            .OnClick(() =>
                            {
                                var next = parameters.Value.ToList();
                                var preset = ResolvePreset(parameterName.Value);
                                next.Add(new ParameterDraft
                                {
                                    Name = preset.Key,
                                    Label = preset.Label,
                                    Value = parameterValue.Value,
                                    Unit = parameterUnit.Value,
                                    Category = parameterCategory.Value,
                                    Source = parameterSource.Value,
                                    Description = parameterDescription.Value,
                                    MinAcceptable = useDefaultThresholds.Value ? null : parameterMin.Value,
                                    MaxAcceptable = useDefaultThresholds.Value ? null : parameterMax.Value,
                                    Weight = useDefaultThresholds.Value ? null : parameterWeight.Value,
                                    IsCritical = isCritical.Value
                                });
                                parameters.Set(next);
                                isOpen.Set(false);
                            }),
                        title: "Add Parameter",
                        description: "Configure parameter limits, category, source and criticality.")
                        .Width(Size.Fraction(1/2f))
                    : null);

            UseEffect(() =>
            {
                var preset = ResolvePreset(parameterName.Value);
                parameterValue.Set(preset.DefaultValue);
                parameterUnit.Set(preset.DefaultUnit);
                parameterCategory.Set(preset.DefaultCategory);
                parameterSource.Set(preset.DefaultSource);
                parameterMin.Set(preset.DefaultMin);
                parameterMax.Set(preset.DefaultMax);
                parameterWeight.Set(preset.DefaultWeight);
                isCritical.Set(preset.DefaultCritical);
                previousUnit.Set(preset.DefaultUnit);
                manualMetadataOverride.Set(false);
            }, parameterName);

            UseEffect(() =>
            {
                var preset = ResolvePreset(parameterName.Value);
                if (parameterUnit.Value == previousUnit.Value)
                {
                    return;
                }

                parameterValue.Set(ConvertFromDefaultUnit(preset, preset.DefaultValue, parameterUnit.Value));
                parameterMin.Set(ConvertFromDefaultUnit(preset, preset.DefaultMin, parameterUnit.Value));
                parameterMax.Set(ConvertFromDefaultUnit(preset, preset.DefaultMax, parameterUnit.Value));
                previousUnit.Set(parameterUnit.Value);
            }, parameterUnit, parameterName);

            return new Fragment()
                   | new BladeHeader(
                       Layout.Horizontal().Gap(2)
                       | new Button("View History")
                           .OnClick(() => navigator.Navigate(typeof(ExperimentHistoryApp)))
                       | new Button("Parameter Catalog")
                           .OnClick(() => blades.Push(this, new ParameterCatalogBlade(), "Parameter Catalog", width: Size.Units(90))))
                   | Layout.Vertical().Padding(4).Gap(2)
                       | Text.H2("Experiment Input Workspace")
                       | new Card(
                           Layout.Vertical()
                           | Text.H3("Experiment Context")
                           | title.ToTextInput().Placeholder("Title")
                           | experimentType.ToSelectInput(ExperimentTypeOptions)
                           | description.ToTextInput().Placeholder("Description"))
                       | new Card(
                           Layout.Vertical()
                           | Text.H3("Parameters Scenario")
                           | new Button("Add Parameter via Sheet")
                               .Primary()
                               .OnClick(_ => showSheet())
                           | (parameters.Value.Count == 0
                               ? Text.Muted("No parameters yet. Add at least one parameter before creating experiment.")
                               : string.Join(Environment.NewLine, parameters.Value.Select((x, idx) =>
                                   $"{idx + 1}. {x.Label}={x.Value} {x.Unit} | category={x.Category} | source={x.Source} | critical={x.IsCritical}")))
                           | sheetView)
                       | new Card(
                           Layout.Vertical()
                           | Text.H3("Finalize")
                           | Text.Muted("Create experiment is available only at the final step.")
                           | new Button("Create Experiment")
                               .Primary()
                               .OnClick(async () =>
                               {
                                   try
                                   {
                                       if (string.IsNullOrWhiteSpace(title.Value))
                                       {
                                           throw new InvalidOperationException("Experiment title is required.");
                                       }

                                       if (parameters.Value.Count == 0)
                                       {
                                           throw new InvalidOperationException("Add at least one parameter.");
                                       }

                                       var experiment = await apiClient.CreateExperimentAsync(new SmartEnergyExpert.Client.Services.CreateExperimentRequestDto
                                       {
                                           Title = title.Value,
                                           ExperimentType = experimentType.Value,
                                           Description = description.Value,
                                           CreatedBy = Guid.Empty
                                       });

                                       foreach (var parameter in parameters.Value)
                                       {
                                           await apiClient.AddParameterAsync(experiment.Id, new SmartEnergyExpert.Client.Services.AddParameterRequestDto
                                           {
                                               ParameterName = parameter.Name,
                                               Value = parameter.Value,
                                               Unit = parameter.Unit,
                                               MinAcceptable = parameter.MinAcceptable,
                                               MaxAcceptable = parameter.MaxAcceptable,
                                               Weight = parameter.Weight,
                                               Category = parameter.Category,
                                               Description = parameter.Description,
                                               IsCritical = parameter.IsCritical,
                                               Source = parameter.Source,
                                               MeasuredAt = DateTimeOffset.UtcNow
                                           });
                                       }

                                       statusMessage.Set($"Experiment created with {parameters.Value.Count} parameters.");
                                       title.Set("");
                                       description.Set("");
                                       parameters.Set([]);
                                   }
                                   catch (Exception ex)
                                   {
                                       statusMessage.Set($"Create failed: {ex.Message}");
                                   }
                               }))
                       | (string.IsNullOrWhiteSpace(statusMessage.Value) ? new Fragment() : Callout.Info(statusMessage.Value));
        }
    }

    private sealed class ParameterCatalogBlade : ViewBase
    {
        public override object? Build()
        {
            var grouped = new
            {
                Physical = "temperature, pressure, voltage, current, power, frequency, vibration, rotation_speed, humidity, coolant_level",
                Reliability = "operating_hours, failure_count, last_maintenance_days, wear_level, sensor_health, component_load_percent",
                Network = "packet_loss, latency_ms, unauthorized_access_attempts, communication_status, data_integrity_score, system_response_time",
                Expert = "expert_confidence, anomaly_level, risk_probability, impact_level, decision_priority"
            };

            return Layout.Vertical().Padding(4)
                   | Text.H3("Parameter Catalog")
                   | grouped.ToDetails().Multiline(x => x.Physical).Multiline(x => x.Reliability).Multiline(x => x.Network).Multiline(x => x.Expert);
        }
    }

    private sealed record ParameterDraft
    {
        public string Name { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public decimal Value { get; init; }
        public string Unit { get; init; } = string.Empty;
        public decimal? MinAcceptable { get; init; }
        public decimal? MaxAcceptable { get; init; }
        public decimal? Weight { get; init; }
        public string Category { get; init; } = "physical";
        public string? Description { get; init; }
        public bool IsCritical { get; init; }
        public string Source { get; init; } = "manual";
    }

    private sealed record ParameterPreset(
        string Key,
        string Label,
        string DefaultUnit,
        string[] Units,
        decimal DefaultValue,
        decimal DefaultMin,
        decimal DefaultMax,
        decimal DefaultWeight,
        string DefaultCategory,
        string DefaultSource,
        bool DefaultCritical);

    private static ParameterPreset ResolvePreset(string label) =>
        ParameterPresets.FirstOrDefault(x => x.Label == label) ?? ParameterPresets[0];

    private static decimal ConvertFromDefaultUnit(ParameterPreset preset, decimal valueInDefaultUnit, string targetUnit)
    {
        if (string.Equals(targetUnit, preset.DefaultUnit, StringComparison.OrdinalIgnoreCase))
        {
            return valueInDefaultUnit;
        }

        var converted = preset.Key switch
        {
            "pressure" => ConvertPressureFromKpa(valueInDefaultUnit, targetUnit),
            "temperature" => ConvertTemperatureFromCelsius(valueInDefaultUnit, targetUnit),
            "voltage" => ConvertVoltageFromVolts(valueInDefaultUnit, targetUnit),
            "current" => ConvertCurrentFromAmperes(valueInDefaultUnit, targetUnit),
            "power" => ConvertPowerFromKw(valueInDefaultUnit, targetUnit),
            "frequency" => ConvertFrequencyFromHz(valueInDefaultUnit, targetUnit),
            "vibration" => ConvertVibrationFromMillimetersPerSecond(valueInDefaultUnit, targetUnit),
            "rotation_speed" => ConvertRotationFromRpm(valueInDefaultUnit, targetUnit),
            "operating_hours" => ConvertDurationFromHours(valueInDefaultUnit, targetUnit),
            "last_maintenance_days" => ConvertDurationFromDays(valueInDefaultUnit, targetUnit),
            "latency_ms" or "system_response_time" => ConvertTimeFromMilliseconds(valueInDefaultUnit, targetUnit),
            _ => valueInDefaultUnit
        };

        return decimal.Round(converted, 3, MidpointRounding.AwayFromZero);
    }

    private static decimal ConvertPressureFromKpa(decimal kpa, string targetUnit) =>
        targetUnit switch
        {
            "kPa" => kpa,
            "Pa" => kpa * 1000m,
            "bar" => kpa / 100m,
            "atm" => kpa / 101.325m,
            _ => kpa
        };

    private static decimal ConvertTemperatureFromCelsius(decimal celsius, string targetUnit) =>
        targetUnit switch
        {
            "C" => celsius,
            "F" => (celsius * 9m / 5m) + 32m,
            "K" => celsius + 273.15m,
            _ => celsius
        };

    private static decimal ConvertPowerFromKw(decimal kw, string targetUnit) =>
        targetUnit switch
        {
            "kW" => kw,
            "W" => kw * 1000m,
            "MW" => kw / 1000m,
            _ => kw
        };

    private static decimal ConvertVoltageFromVolts(decimal volts, string targetUnit) =>
        targetUnit switch
        {
            "V" => volts,
            "kV" => volts / 1000m,
            _ => volts
        };

    private static decimal ConvertCurrentFromAmperes(decimal amperes, string targetUnit) =>
        targetUnit switch
        {
            "A" => amperes,
            "mA" => amperes * 1000m,
            _ => amperes
        };

    private static decimal ConvertFrequencyFromHz(decimal hz, string targetUnit) =>
        targetUnit switch
        {
            "Hz" => hz,
            "kHz" => hz / 1000m,
            _ => hz
        };

    private static decimal ConvertVibrationFromMillimetersPerSecond(decimal millimetersPerSecond, string targetUnit) =>
        targetUnit switch
        {
            "mm/s" => millimetersPerSecond,
            "m/s" => millimetersPerSecond / 1000m,
            _ => millimetersPerSecond
        };

    private static decimal ConvertRotationFromRpm(decimal rpm, string targetUnit) =>
        targetUnit switch
        {
            "rpm" => rpm,
            "rps" => rpm / 60m,
            _ => rpm
        };

    private static decimal ConvertDurationFromHours(decimal hours, string targetUnit) =>
        targetUnit switch
        {
            "h" => hours,
            "days" => hours / 24m,
            _ => hours
        };

    private static decimal ConvertDurationFromDays(decimal days, string targetUnit) =>
        targetUnit switch
        {
            "days" => days,
            "h" => days * 24m,
            _ => days
        };

    private static decimal ConvertTimeFromMilliseconds(decimal milliseconds, string targetUnit) =>
        targetUnit switch
        {
            "ms" => milliseconds,
            "s" => milliseconds / 1000m,
            _ => milliseconds
        };
}
