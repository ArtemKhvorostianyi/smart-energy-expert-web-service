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

    private static readonly string[] ParameterNameOptions =
    [
        "temperature", "pressure", "voltage", "current", "power", "frequency", "vibration", "rotation_speed", "humidity", "coolant_level",
        "operating_hours", "failure_count", "last_maintenance_days", "wear_level", "sensor_health", "component_load_percent",
        "packet_loss", "latency_ms", "unauthorized_access_attempts", "communication_status", "data_integrity_score", "system_response_time",
        "expert_confidence", "anomaly_level", "risk_probability", "impact_level", "decision_priority"
    ];

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
            var parameterValue = UseState(45m);
            var parameterUnit = UseState("C");
            var parameterCategory = UseState(CategoryOptions[0]);
            var parameterSource = UseState(SourceOptions[1]);
            var parameterDescription = UseState("");
            var parameterMin = UseState(10m);
            var parameterMax = UseState(60m);
            var parameterWeight = UseState(0.25m);
            var useDefaultThresholds = UseState(true);
            var isCritical = UseState(false);
            var blades = UseContext<IBladeContext>();
            var (sheetView, showSheet) = UseTrigger((IState<bool> isOpen) =>
                isOpen.Value
                    ? new Sheet(_ => isOpen.Set(false),
                        Layout.Vertical()
                        | parameterName.ToSelectInput(ParameterNameOptions)
                        | parameterValue.ToNumberInput().Placeholder("Value")
                        | parameterUnit.ToTextInput().Placeholder("Unit")
                        | parameterDescription.ToTextInput().Placeholder("Description (optional)")
                        | parameterCategory.ToSelectInput(CategoryOptions)
                        | parameterSource.ToSelectInput(SourceOptions)
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
                                next.Add(new ParameterDraft
                                {
                                    Name = parameterName.Value,
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
                                   $"{idx + 1}. {x.Name}={x.Value} {x.Unit} | category={x.Category} | source={x.Source} | critical={x.IsCritical}")))
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
}
