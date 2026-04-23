namespace SmartEnergyExpert.Client.Apps;

[App(icon: Icons.History, title: "Experiment History", searchHints: ["history", "experiments", "details", "blades"])]
public sealed class ExperimentHistoryApp : ViewBase
{
    public override object? Build()
    {
        var bladesView = UseBlades(() => new ExperimentHistoryRootBlade(), "Experiment History");
        return bladesView;
    }
}

public sealed class ExperimentHistoryRootBlade : ViewBase
{
    public override object? Build()
    {
        var apiClient = UseService<SmartEnergyExpert.Client.Services.IApiClient>();
        var blades = UseContext<IBladeContext>();
        var experimentsQuery = UseQuery(
            key: nameof(ExperimentHistoryRootBlade),
            fetcher: async ct => await apiClient.GetExperimentsAsync(ct));
        var experiments = experimentsQuery.Value ?? [];

        if (experimentsQuery.Loading)
        {
            return Skeleton.Card();
        }

        if (experimentsQuery.Error is { } err)
        {
            return Callout.Error(err.Message);
        }

        var items = experiments.Select(x =>
            new ListItem($"{x.Title} | {x.ExperimentType} | {x.Status} | {x.CreatedAt:yyyy-MM-dd HH:mm}",
                onClick: _ => blades.Push(this, new ExperimentHistoryDetailsBlade(x), x.Title, width: Size.Units(100))));

        return Layout.Vertical().Padding(4)
               | Text.H3("All Experiments")
               | new List(items);
    }
}

public sealed class ExperimentHistoryDetailsBlade(SmartEnergyExpert.Client.Services.ExperimentDto experiment) : ViewBase
{
    public override object? Build()
    {
        var apiClient = UseService<SmartEnergyExpert.Client.Services.IApiClient>();
        var blades = UseContext<IBladeContext>();
        var parametersQuery = UseQuery(
            key: (nameof(ExperimentHistoryDetailsBlade), experiment.Id),
            fetcher: async ct => await apiClient.GetParametersAsync(experiment.Id, ct));
        var parameters = parametersQuery.Value ?? [];

        return Layout.Vertical().Padding(4).Gap(2)
               | Text.H3("Experiment Details")
               | new
               {
                   experiment.Id,
                   experiment.Title,
                   experiment.ExperimentType,
                   experiment.Status,
                   experiment.Description,
                   experiment.CreatedAt
               }.ToDetails().RemoveEmpty().Multiline(x => x.Description).Builder(x => x.Id, b => b.CopyToClipboard())
               | Text.H3("Parameters")
               | (parametersQuery.Loading
                   ? Skeleton.Card()
                   : parametersQuery.Error is { } err
                       ? Callout.Error(err.Message)
                       : new List(parameters.Select(p =>
                           new ListItem($"{p.ParameterName} ({p.Category}) = {p.Value} {p.Unit}",
                               onClick: _ => blades.Push(this, new ParameterDetailsBlade(p), p.ParameterName, width: Size.Units(80))))));
    }
}

public sealed class ParameterDetailsBlade(SmartEnergyExpert.Client.Services.ExperimentParameterDto parameter) : ViewBase
{
    public override object? Build()
    {
        return Layout.Vertical().Padding(4)
               | Text.H3("Parameter Details")
               | new
               {
                   parameter.ParameterName,
                   parameter.Value,
                   parameter.Unit,
                   parameter.MinAcceptable,
                   parameter.MaxAcceptable,
                   parameter.Weight,
                   parameter.Category,
                   parameter.Source,
                   parameter.IsCritical,
                   parameter.Description,
                   parameter.MeasuredAt
               }.ToDetails().RemoveEmpty().Multiline(x => x.Description);
    }
}
