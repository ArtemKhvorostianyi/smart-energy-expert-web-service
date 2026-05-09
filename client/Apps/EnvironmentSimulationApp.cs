using System.Collections.Immutable;
using ClientServices = SmartEnergyExpert.Client.Services;

namespace SmartEnergyExpert.Client.Apps;

[App(
    icon: Icons.Waves,
    title: "Середовищна симуляція",
    group: ["Датасети"],
    searchHints: ["середовище", "симуляція", "синтетичні", "параметри", "температура", "солоність", "глибина", "шум", "модель"])]
public sealed class EnvironmentSimulationApp : ViewBase
{
    private const int SamplePageSize = 150;
    private const float TabularPreviewHeightFraction = 0.2f;

    private static string FieldAlignIndependentOption() =>
        $"— Незалежна сітка (тривалість і смуги нижче) — [{Guid.Empty}]";

    private static string ToFieldMirrorOption(ClientServices.DatasetDto dataset) =>
        $"{dataset.Name} | {dataset.SourceSystem} | {dataset.SampleCount} зразків [{dataset.Id}]";

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
            throw new InvalidOperationException("Невірне значення датасету.");
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
            ? Text.Muted("Датасетів ще немає — згенеруйте один вище.")
            : BuildSessionSimulationsTable(generatedSimulationRows.Value);

        object sampleRowsPanel = BuildSampleRowsPanel(
            samplesPageQuery,
            previewSamplesDatasetId.Value,
            samplesOffset);

        return Layout.Vertical().Gap(2)
               | Text.H2("Симуляція на основі середовища")
               | Text.P(
                   "Задайте параметри водяного стовпа та ґрунту дна; сервіс будує евристичну синтетичну SPL-серію "
                   + "(тип датасету simulation). Після імпорту вимірювань (зокрема довгих ARLUT CSV) як поле «field» нижче "
                   + "за потреби віддзеркаліть його мітки часу та смуги частот — тоді в «Гідроакустичному порівнянні» узгодяться UTC × частота.")

               | new Card(
                   Layout.Vertical().Gap(1)
                   | Text.H3("Середовище")
                   | simName.ToTextInput().Placeholder("Базова назва симуляції (за потреби додається суфікс)")
                   | Text.Muted("Віддзеркалити мітки часу та смуги з імпортованого поля (необов’язково)")
                   | alignFieldSelection.ToSelectInput(alignOptionsArray.ToArray())
                   | (mirrorsFieldDataset
                       ? Text.Muted(
                           "На виході один зразок на акустичний ряд поля; тривалість і список смуг нижче ігноруються. "
                           + "SPL замінюється евристикою; геометрія наслідує вимірювання.")
                       : new Fragment())
                   | Text.Muted("Глибина (м)")
                   | depthM.ToNumberInput(min: 1, max: 12_000)
                   | Text.Muted("Температура (°C)")
                   | temperatureC.ToNumberInput(min: -2, max: 40)
                   | Text.Muted("Солоність (PSU)")
                   | salinityPsu.ToNumberInput(min: 0, max: 45)
                   | Text.Muted("Шумова підкладка (дБ щодо 1 µPa, ілюстраційно)")
                   | noiseLevelDb.ToNumberInput(min: -120, max: -20)
                   | Text.Muted(
                       "Тип дна")
                   | bottomType.ToSelectInput(SimulationBottomTypes)
                   | Text.Muted(
                       mirrorsFieldDataset
                           ? "Тривалість (хв) — не використовується при віддзеркаленні поля"
                           : "Тривалість (хв)")
                   | durationMin.ToNumberInput(min: 1, max: 240)
                   | new Button("Створити датасет симуляції").Primary().Disabled(busy.Value).OnClick(async () =>
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
                               $"Готово: «{ds.Name}» — {ds.SampleCount} зразків, {ds.SourceSystem}, id {ds.Id}. "
                               + "Оберіть його в «Гідроакустичному порівнянні» як Симуляцію.");
                       }
                       catch (Exception ex)
                       {
                           status.Set($"Помилка генерації: {ex.Message}");
                       }
                       finally
                       {
                           busy.Set(false);
                       }
                   }))

               | new Card(
                   Layout.Vertical().Gap(1)
                   | Text.H3("Датасети симуляції в цій сесії")
                   | Text.Muted(
                       "Табличний огляд датасетів у поточній сесії.")
                   | simulationTablePanel)

               | new Card(
                   Layout.Vertical().Gap(1)
                   | Text.H3("Створений датасет — перші рядки")
                   | Text.Muted(
                       "Порціоновані акустичні зразки з API (ті самі поля, що CSV). Після успішної генерації тут показано останній датасет.")
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
            return Text.Muted("Створіть датасет вище — тоді з’являться його акустичні зразки.");
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
            return Text.Muted("Немає даних сторінки.");
        }

        if (page.Items.Length == 0)
        {
            return Text.Muted(page.TotalCount == 0
                ? "У цьому датасеті немає акустичних зразків."
                : "У цьому вікні зміщення рядів немає — спробуйте «Назад».");
        }

        var showingEnd = Math.Min(page.Offset + page.Items.Length, page.TotalCount);
        return Layout.Vertical().Gap(1)
               | Text.Block(page.DatasetName).Bold()
               | Text.Muted($"Рядки {page.Offset + 1}–{showingEnd} з {page.TotalCount} (розмір сторінки {SamplePageSize}).")
               | (Layout.Horizontal().Gap(2)
                   | new Button("Назад")
                       .Disabled(page.Offset <= 0)
                       .OnClick(() => samplesOffsetState.Set(Math.Max(0, samplesOffsetState.Value - SamplePageSize)))
                   | new Button("Далі")
                       .Disabled(showingEnd >= page.TotalCount)
                       .OnClick(() => samplesOffsetState.Set(samplesOffsetState.Value + SamplePageSize)))
               | BuildAcousticSamplesTable(page.Items);
    }

    private sealed record AcousticSamplePreviewRow(
        DateTimeOffset TimestampUtc,
        decimal FrequencyBandHz,
        decimal AmplitudeDb,
        decimal DepthMeters,
        decimal RangeMeters,
        decimal? SoundSpeed,
        decimal? NoiseLevelDb);

    private static object BuildAcousticSamplesTable(IReadOnlyList<ClientServices.AcousticSampleRowDto> rows)
    {
        var data = rows
            .Select(r => new AcousticSamplePreviewRow(
                r.Timestamp,
                r.FrequencyBand,
                r.AmplitudeDb,
                r.DepthMeters,
                r.RangeMeters,
                r.SoundSpeed,
                r.NoiseLevelDb))
            .ToArray();

        var grid = data.ToTable()
            .Header(x => x.TimestampUtc, "Мітка часу (UTC)")
            .Header(x => x.FrequencyBandHz, "f (Гц)")
            .Header(x => x.AmplitudeDb, "Амплітуда (дБ)")
            .Header(x => x.DepthMeters, "Глибина (м)")
            .Header(x => x.RangeMeters, "Дальність (м)")
            .Header(x => x.SoundSpeed, "Швидкість звуку")
            .Header(x => x.NoiseLevelDb, "Шум (дБ)")
            .Width(Size.Full());

        return (Layout.Vertical()
                .Height(Size.Fraction(TabularPreviewHeightFraction))
                .Scroll(Scroll.Vertical))
               | grid;
    }

    private static object BuildSessionSimulationsTable(ImmutableArray<SimulatedDatasetGridRow> rows)
    {
        var data = rows.OrderBy(r => r.Name).ToArray();
        var grid = data.ToTable()
            .Header(r => r.Name, "Датасет")
            .Header(r => r.SampleCount, "Зразків")
            .Header(r => r.SourceSystem, "Джерело")
            .Header(r => r.Type, "Тип")
            .Header(r => r.Id, "ID датасету")
            .Header(r => r.TimeRangeStart, "Початок періоду")
            .Header(r => r.TimeRangeEnd, "Кінець періоду")
            .Width(Size.Full());

        return (Layout.Vertical()
                .Height(Size.Fraction(TabularPreviewHeightFraction))
                .Scroll(Scroll.Vertical))
               | grid;
    }
}
