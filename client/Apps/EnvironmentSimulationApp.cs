using System.Collections.Immutable;
using ClientServices = SmartEnergyExpert.Client.Services;

namespace SmartEnergyExpert.Client.Apps;

[App(
    icon: Icons.Waves,
    title: "Environment simulation",
    group: ["Datasets"],
    searchHints: ["simulation", "synthetic", "parameter", "temperature", "salinity", "depth", "bottom", "noise", "model"])]
public sealed class EnvironmentSimulationApp : ViewBase
{
    private const int SamplePageSize = 150;

    private static string FieldAlignIndependentOption() =>
        $"— Independent grid (duration & bands below) — [{Guid.Empty}]";

    private static string ToFieldMirrorOption(ClientServices.DatasetDto dataset) =>
        $"{dataset.Name} | {dataset.SourceSystem} | {dataset.SampleCount} samples [{dataset.Id}]";

    private static Guid TryParseMirroredFieldId(string value)
    {
        try
        {
            var id = ParseDatasetBracketId(value);
            return id == Guid.Empty ? Guid.Empty : id;
        }
        catch
        {
            return Guid.Empty;
        }
    }

    private static Guid ParseDatasetBracketId(string value)
    {
        var openIndex = value.LastIndexOf('[');
        var closeIndex = value.LastIndexOf(']');
        if (openIndex < 0 || closeIndex <= openIndex)
        {
            throw new InvalidOperationException("Invalid dataset value.");
        }

        return Guid.Parse(value.Substring(openIndex + 1, closeIndex - openIndex - 1));
    }

    private static readonly string[] SimulationBottomTypes =
        ["sand", "mud", "silt", "clay_mud", "hard_rock", "rock", "granite"];

    private sealed record SimulatedDatasetGridRow(
        Guid Id,
        string Name,
        string Type,
        string SourceSystem,
        int SampleCount,
        DateTimeOffset TimeRangeStart,
        DateTimeOffset TimeRangeEnd);

    public override object? Build()
    {
        var api = UseService<ClientServices.IApiClient>();
        var status = UseState("");
        var busy = UseState(false);
        var generatedSimulationRows = UseState(ImmutableArray<SimulatedDatasetGridRow>.Empty);
        var previewSamplesDatasetId = UseState(Guid.Empty);
        var samplesOffset = UseState(0);

        var simName = UseState("parameter-simulation");
        var alignFieldSelection = UseState(FieldAlignIndependentOption());
        var depthM = UseState(60m);
        var temperatureC = UseState(12m);
        var salinityPsu = UseState(35m);
        var noiseLevelDb = UseState(-92m);
        var bottomType = UseState("sand");
        var durationMin = UseState(60m);

        var datasetsQuery =
            UseQuery(key: $"{nameof(EnvironmentSimulationApp)}:datasets", fetcher: api.GetDatasetsAsync);

        var samplesPageQuery = UseQuery(
            key: ("env-sim-samples", previewSamplesDatasetId.Value, samplesOffset.Value),
            fetcher: async ct =>
            {
                if (previewSamplesDatasetId.Value == Guid.Empty)
                {
                    return (ClientServices.DatasetSamplesPageDto?)null;
                }

                return await api.GetDatasetSamplesPageAsync(
                    previewSamplesDatasetId.Value,
                    samplesOffset.Value,
                    SamplePageSize,
                    ct);
            });

        var alignOptions = ImmutableArray.CreateBuilder<string>();
        alignOptions.Add(FieldAlignIndependentOption());
        if (datasetsQuery.Value is { } datasets)
        {
            foreach (var ds in datasets
                         .Where(x => string.Equals(x.Type, "field", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(x => x.Name))
            {
                alignOptions.Add(ToFieldMirrorOption(ds));
            }
        }

        var alignOptionsArray = alignOptions.ToImmutable();
        var mirrorsFieldDataset =
            TryParseMirroredFieldId(alignFieldSelection.Value) != Guid.Empty;

        object simulationTablePanel = generatedSimulationRows.Value.IsEmpty
            ? Text.Muted("No datasets yet — generate one above.")
            : BuildSessionSimulationsTable(generatedSimulationRows.Value);

        object sampleRowsPanel = BuildSampleRowsPanel(
            samplesPageQuery,
            previewSamplesDatasetId.Value,
            samplesOffset);

        return Layout.Vertical().Gap(2)
               | Text.H2("Environment-based simulation")
               | Text.P(
                   "Define water-column and seabed assumptions; the service writes a heuristic synthetic SPL series "
                   + "(dataset type simulation). After you import measurements (including long ARLUT CSVs) as a field dataset "
                   + "below, optionally mirror its timestamps and bands so Hydroacoustic Comparison lines up matching UTC × frequency rows.")

               | new Card(
                   Layout.Vertical().Gap(1)
                   | Text.H3("Environment")
                   | simName.ToTextInput().Placeholder("Simulation name stem (unique suffix added if needed)")
                   | Text.Muted("Mirror timestamps & bands from imported field dataset (optional)")
                   | alignFieldSelection.ToSelectInput(alignOptionsArray.ToArray())
                   | (mirrorsFieldDataset
                       ? Text.Muted(
                           "Synthetic output has one sample per acoustic row in that field dataset; duration and frequency band list "
                           + "below are ignored. SPL is replaced by the heuristic; geometry columns follow the measurements.")
                       : new Fragment())
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
                   | Text.Muted(
                       mirrorsFieldDataset ? "Duration (minutes) — not used while mirroring" : "Duration (minutes)")
                   | durationMin.ToNumberInput(min: 1, max: 240)
                   | new Button("Generate simulation dataset").Primary().Disabled(busy.Value).OnClick(async () =>
                   {
                       busy.Set(true);
                       try
                       {
                           var parsedMirror = TryParseMirroredFieldId(alignFieldSelection.Value);
                           Guid? alignId = parsedMirror == Guid.Empty ? null : parsedMirror;
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
                               DurationMinutes = (int)decimal.Round(decimal.Clamp(durationMin.Value, 1, 240)),
                               AlignToFieldDatasetId = alignId
                           };
                           var ds = await api.GenerateSimulationDatasetAsync(dto);
                           generatedSimulationRows.Set(generatedSimulationRows.Value.Add(new SimulatedDatasetGridRow(
                               ds.Id,
                               ds.Name,
                               ds.Type,
                               ds.SourceSystem,
                               ds.SampleCount,
                               ds.TimeRangeStart,
                               ds.TimeRangeEnd)));
                           previewSamplesDatasetId.Set(ds.Id);
                           samplesOffset.Set(0);
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

               | new Card(
                   Layout.Vertical().Gap(1)
                   | Text.H3("Session simulation datasets")
                   | Text.Muted(
                       "Tabular overview of datasets produced in this Ivy session (Ivy Table widget). "
                       + "See also https://docs.ivy.app/widgets/common/table.md and https://docs.ivy.app/widgets/advanced/data-table#datatable")
                   | simulationTablePanel)

               | new Card(
                   Layout.Vertical().Gap(1)
                   | Text.H3("Generated dataset — sample rows")
                   | Text.Muted(
                       "Paged acoustic samples from the API (same fields as CSV import). "
                       + "After each successful generation, the latest dataset is loaded here.")
                   | sampleRowsPanel)

               | (string.IsNullOrWhiteSpace(status.Value) ? new Fragment() : Callout.Info(status.Value));
    }

    private object BuildSampleRowsPanel(
        QueryResult<ClientServices.DatasetSamplesPageDto?> samplesPageQuery,
        Guid previewDatasetId,
        IState<int> samplesOffsetState)
    {
        if (previewDatasetId == Guid.Empty)
        {
            return Text.Muted("Generate a dataset above to fetch and display its acoustic samples.");
        }

        if (samplesPageQuery.Loading)
        {
            return Skeleton.Card();
        }

        if (samplesPageQuery.Error is { } err)
        {
            return Callout.Warning(err.Message);
        }

        var page = samplesPageQuery.Value;
        if (page is null)
        {
            return Text.Muted("No page data.");
        }

        if (page.Items.Length == 0)
        {
            return Text.Muted(page.TotalCount == 0
                ? "This dataset has no acoustic samples."
                : "No rows in this offset window — try Previous.");
        }

        var showingEnd = Math.Min(page.Offset + page.Items.Length, page.TotalCount);
        return Layout.Vertical().Gap(1)
               | Text.Block(page.DatasetName).Bold()
               | Text.Muted($"Rows {page.Offset + 1}–{showingEnd} of {page.TotalCount} (page size {SamplePageSize}).")
               | (Layout.Horizontal().Gap(2)
                   | new Button("Previous")
                       .Disabled(page.Offset <= 0)
                       .OnClick(() => samplesOffsetState.Set(Math.Max(0, samplesOffsetState.Value - SamplePageSize)))
                   | new Button("Next")
                       .Disabled(showingEnd >= page.TotalCount)
                       .OnClick(() => samplesOffsetState.Set(samplesOffsetState.Value + SamplePageSize)))
               | BuildAcousticSamplesTable(page.Items);
    }

    private static Table BuildAcousticSamplesTable(IReadOnlyList<ClientServices.AcousticSampleRowDto> rows)
    {
        var header = new TableRow(
            new TableCell(Text.Block("Timestamp (UTC)").Bold()),
            new TableCell(Text.Block("f (Hz)").Bold()),
            new TableCell(Text.Block("Amplitude (dB)").Bold()),
            new TableCell(Text.Block("Depth (m)").Bold()),
            new TableCell(Text.Block("Range (m)").Bold()),
            new TableCell(Text.Block("Sound speed").Bold()),
            new TableCell(Text.Block("Noise (dB)").Bold()));

        var table = new Table(header);
        foreach (var r in rows)
        {
            table |= new TableRow(
                new TableCell(Text.Block(r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"))),
                new TableCell(Text.Block(r.FrequencyBand.ToString("G29"))),
                new TableCell(Text.Block(r.AmplitudeDb.ToString("F2"))),
                new TableCell(Text.Block(r.DepthMeters.ToString("F2"))),
                new TableCell(Text.Block(r.RangeMeters.ToString("F1"))),
                new TableCell(Text.Block(r.SoundSpeed?.ToString("F2") ?? "—")),
                new TableCell(Text.Block(r.NoiseLevelDb?.ToString("F2") ?? "—")));
        }

        return table;
    }

    private static Table BuildSessionSimulationsTable(ImmutableArray<SimulatedDatasetGridRow> rows)
    {
        var header = new TableRow(
            new TableCell(Text.Block("Dataset").Bold()),
            new TableCell(Text.Block("Samples").Bold()),
            new TableCell(Text.Block("Source").Bold()),
            new TableCell(Text.Block("Type").Bold()),
            new TableCell(Text.Block("Dataset id").Bold()),
            new TableCell(Text.Block("Period start").Bold()),
            new TableCell(Text.Block("Period end").Bold()));

        var table = new Table(header);
        foreach (var r in rows)
        {
            table |= new TableRow(
                new TableCell(Text.Block(r.Name)),
                new TableCell(Text.Block(r.SampleCount.ToString())),
                new TableCell(Text.Block(r.SourceSystem)),
                new TableCell(Text.Block(r.Type)),
                new TableCell(Text.Block(r.Id.ToString())),
                new TableCell(Text.Block(r.TimeRangeStart.ToString("yyyy-MM-dd HH:mm"))),
                new TableCell(Text.Block(r.TimeRangeEnd.ToString("yyyy-MM-dd HH:mm"))));
        }

        return table;
    }
}
