namespace SmartEnergyExpert.Client.Apps;

[App(icon: Icons.FlaskConical, title: "Experiments", searchHints: ["experiments", "parameters", "input", "measurements"])]
public sealed class ExperimentsApp : ViewBase
{
    public override object? Build()
    {
        var apiClient = UseService<SmartEnergyExpert.Client.Services.IApiClient>();
        var title = UseState("");
        var experimentType = UseState("default");
        var description = UseState("");
        var selectedExperimentId = UseState<Guid?>(null);
        var parameterName = UseState("temperature");
        var parameterValue = UseState("45");
        var parameterUnit = UseState("C");
        var statusMessage = UseState("");
        var refreshTick = UseState(0);

        var experimentsQuery = UseQuery(
            key: (nameof(ExperimentsApp), refreshTick.Value),
            fetcher: async ct => await apiClient.GetExperimentsAsync(ct));
        var experiments = experimentsQuery.Value ?? [];

        return Layout.Vertical().Padding(4).Gap(2)
               | Text.H2("Experiment Input Workspace")
               | new Card(
                   Layout.Vertical()
                   | Text.H3("Create Experiment")
                   | title.ToTextInput().Placeholder("Title")
                   | experimentType.ToTextInput().Placeholder("Experiment type")
                   | description.ToTextInput().Placeholder("Description")
                   | new Button("Create Experiment")
                       .Primary()
                       .OnClick(async () =>
                       {
                           try
                           {
                               await apiClient.CreateExperimentAsync(new SmartEnergyExpert.Client.Services.CreateExperimentRequestDto
                               {
                                   Title = title.Value,
                                   ExperimentType = experimentType.Value,
                                   Description = description.Value,
                                   CreatedBy = Guid.Empty
                               });
                               statusMessage.Set("Experiment created.");
                               refreshTick.Set(refreshTick.Value + 1);
                               title.Set("");
                               description.Set("");
                           }
                           catch (Exception ex)
                           {
                               statusMessage.Set($"Create failed: {ex.Message}");
                           }
                       }))
               | new Card(
                   Layout.Vertical()
                   | Text.H3("Add Parameter")
                   | Text.Block((selectedExperimentId.Value ?? experiments.FirstOrDefault()?.Id) is null
                       ? "Selected experiment: not selected"
                       : $"Selected experiment: {selectedExperimentId.Value ?? experiments.First().Id}")
                   | parameterName.ToTextInput().Placeholder("Parameter name")
                   | parameterValue.ToTextInput().Placeholder("Value")
                   | parameterUnit.ToTextInput().Placeholder("Unit")
                   | new Button("Add Parameter")
                       .OnClick(async () =>
                       {
                           try
                           {
                               var experimentId = selectedExperimentId.Value ?? experiments.FirstOrDefault()?.Id;
                               if (experimentId is null)
                               {
                                   throw new InvalidOperationException("Create at least one experiment first.");
                               }

                               if (!decimal.TryParse(parameterValue.Value, out var value))
                               {
                                   throw new InvalidOperationException("Value must be numeric.");
                               }

                               await apiClient.AddParameterAsync(experimentId.Value, new SmartEnergyExpert.Client.Services.AddParameterRequestDto
                               {
                                   ParameterName = parameterName.Value,
                                   Value = value,
                                   Unit = parameterUnit.Value,
                                   MeasuredAt = DateTimeOffset.UtcNow
                               });

                               statusMessage.Set("Parameter added.");
                               refreshTick.Set(refreshTick.Value + 1);
                           }
                           catch (Exception ex)
                           {
                               statusMessage.Set($"Add parameter failed: {ex.Message}");
                           }
                       }))
               | (string.IsNullOrWhiteSpace(statusMessage.Value) ? new Fragment() : Text.Block(statusMessage.Value))
               | Text.H3("Experiment History")
               | (experimentsQuery.Loading
                   ? Skeleton.Card()
                   : experimentsQuery.Error is { } err
                       ? Callout.Error($"Failed to load experiments: {err.Message}")
                       : new Card(
                           Layout.Vertical()
                           | new Button("Use latest experiment for parameter input")
                               .OnClick(() =>
                               {
                                   if (experiments.Count > 0)
                                   {
                                       selectedExperimentId.Set(experiments[0].Id);
                                   }
                               })
                           | string.Join(
                               Environment.NewLine,
                               experiments.Select(x => $"{x.Id} | {x.Title} | {x.ExperimentType} | {x.Status} | {x.CreatedAt:yyyy-MM-dd HH:mm}"))));
    }
}
