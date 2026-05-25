using ClientServices = SmartEnergyExpert.Client.Services;
using SmartEnergyExpert.Client.Reporting;
using SmartEnergyExpert.Client.Services.Auth;

namespace SmartEnergyExpert.Client.Apps;

[App(
    icon: Icons.Waves,
    title: "Гідроакустичне порівняння",
    group: ["Сервіс"],
    order: 30,
    searchHints:
    [
        "гідроакустика",
        "порівняння",
        "графіки",
        "сигнал",
        "explorer",
        "датасет",
        "hydroacoustic",
        "comparison",
        "charts"
    ])]
public sealed class EvaluationsApp : ViewBase
{
    public override object? Build() => UseBlades(() => new WorkspaceBlade(), "Гідроакустичне порівняння");

    private sealed class WorkspaceBlade : ViewBase
    {
        public override object? Build()
        {
            var apiClient = UseService<ClientServices.IApiClient>();
            var auth = UseService<IAuthService>();
            var userQuery = UseQuery(
                key: AuthViewHelper.UserQueryKey,
                fetcher: async ct =>
                {
                    if (auth.GetAuthSession()?.AuthToken is null)
                    {
                        return (UserInfo?)null;
                    }

                    return auth is AuthService authService
                        ? await authService.GetUserInfoAsync(ct)
                        : null;
                });
            var access = AuthAccess.From(auth, userQuery.Value);
            var blades = UseContext<IBladeContext>();
            var refreshTick = UseState(0);
            var selectedSimulation = UseState("");
            var selectedField = UseState("");
            var topN = UseState(15m);
            var status = UseState("");
            var result = UseState<ClientServices.ComparisonResultDto?>(null);

            var datasetsQuery = UseQuery(
                key: (nameof(WorkspaceBlade), refreshTick.Value),
                fetcher: async ct => await apiClient.GetDatasetsAsync(ct));
            var simulationExplorerQuery = UseQuery(
                key: ("sim-signal-explorer", refreshTick.Value, selectedSimulation.Value),
                fetcher: async ct =>
                {
                    var id = TryParseDatasetId(selectedSimulation.Value);
                    if (id == Guid.Empty)
                    {
                        return (ClientServices.DatasetSignalOverviewDto?)null;
                    }

                    return await apiClient.GetDatasetSignalOverviewAsync(id, ct);
                });
            var fieldExplorerQuery = UseQuery(
                key: ("field-signal-explorer", refreshTick.Value, selectedField.Value),
                fetcher: async ct =>
                {
                    var id = TryParseDatasetId(selectedField.Value);
                    if (id == Guid.Empty)
                    {
                        return (ClientServices.DatasetSignalOverviewDto?)null;
                    }

                    return await apiClient.GetDatasetSignalOverviewAsync(id, ct);
                });
            var datasets = datasetsQuery.Value ?? [];
            var simOptions = datasets.Where(x => IsSimulationType(x.Type)).Select(ToOption).ToArray();
            var fieldOptions = datasets.Where(x => IsFieldType(x.Type)).Select(ToOption).ToArray();
            var canRun = !datasetsQuery.Loading
                         && simOptions.Length > 0
                         && fieldOptions.Length > 0
                         && !string.IsNullOrWhiteSpace(selectedSimulation.Value)
                         && !string.IsNullOrWhiteSpace(selectedField.Value);

            void OpenChartsBlade()
            {
                if (result.Value is null)
                {
                    status.Set("Спочатку запустіть порівняння.");
                    return;
                }

                blades.Push(this, new ChartsBlade(result.Value), "Графіки", width: Size.Units(120));
            }

            return new Fragment()
                   | Layout.Vertical().Gap(2)
                       | Text.H2("Гідроакустичне порівняння")
                       | (access.IsGuest
                           ? Callout.Info("Гостьовий режим: порівняння доступне; PDF — після входу з повним профілем.")
                           : access.CanWrite
                               ? new Fragment()
                               : Callout.Info("Увійдіть або зареєструйте профіль для PDF-звіту."))
                       | new Card(
                           Layout.Vertical().Gap(1)
                           | (Layout.Horizontal().Gap(2)
                               | Text.H3("Вибір")
                               | new Button("Оновити список датасетів")
                                   .Disabled(datasetsQuery.Loading)
                                   .OnClick(() => refreshTick.Set(refreshTick.Value + 1)))
                           | Text.Muted("Після імпорту CSV або генерації симуляції натисніть «Оновити», щоб з’явилися нові датасети.")
                           | (datasetsQuery.Loading
                               ? Skeleton.Card()
                               : simOptions.Length == 0
                                   ? Callout.Warning(
                                       "Немає датасетів симуляції. Створіть їх у «Середовищна симуляція» (тип simulation), потім оновіть список.")
                                   : selectedSimulation.ToSelectInput(simOptions))
                           | Text.Muted("Симуляція — лише тип simulation (гілка моделі). Не для CSV ARLUT як симуляція; вимірювання — нижче, поле.")
                           | (datasetsQuery.Loading
                               ? Skeleton.Card()
                               : fieldOptions.Length == 0
                                   ? Callout.Warning(
                                       "Немає полових датасетів. Імпортуйте CSV у «Керування датасетами» або використайте засіяний ARLUT, потім оновіть.")
                                   : selectedField.ToSelectInput(fieldOptions))
                           | Text.Muted(
                               "Поле — вимірювання. При старті застосунку може підвантажуватися bundled CSV як «ARLUT 01 part A field stride2500»."))
                       | BuildSignalExplorerCard(simulationExplorerQuery, fieldExplorerQuery)
                       | new Card(
                           Layout.Vertical()
                           | Text.H3("Порівняти")
                           | topN.ToNumberInput(min: 5, max: 100).Placeholder("N найбільших відхилень")
                           | Text.Muted("Після перегляду сигналів налаштуйте параметр вище — скільки найбільших відхилень аналізувати.")
                           | new Button("Запустити порівняння").Primary().Disabled(!canRun).OnClick(async () =>
                           {
                               try
                               {
                                   if (!canRun)
                                   {
                                       status.Set("Оберіть обидва датасети.");
                                       return;
                                   }

                                   var latest = await apiClient.RunComparisonAsync(new ClientServices.CreateComparisonRequestDto
                                   {
                                       SimulationDatasetId = ParseDatasetId(selectedSimulation.Value),
                                       FieldDatasetId = ParseDatasetId(selectedField.Value),
                                       TopN = (int)decimal.Clamp(topN.Value, 5, 100)
                                   });
                                   result.Set(latest);
                                   status.Set("Порівняння виконано.");
                               }
                               catch (Exception ex)
                               {
                                   status.Set($"Не вдалося порівняти: {ex.Message}");
                               }
                           }))
                       | (datasetsQuery.Error is { } e ? Callout.Warning(e.Message) : new Fragment())
                       | (string.IsNullOrWhiteSpace(status.Value) ? new Fragment() : Callout.Info(status.Value))
                       | (result.Value is null
                           ? new Fragment()
                           : new ComparisonResultsSection(
                               result.Value,
                               OpenChartsBlade,
                               selectedSimulation.Value,
                               selectedField.Value,
                               simulationExplorerQuery.Value,
                               fieldExplorerQuery.Value));
        }

        private static object BuildSignalExplorerCard(
            QueryResult<ClientServices.DatasetSignalOverviewDto?> simulationExplorerQuery,
            QueryResult<ClientServices.DatasetSignalOverviewDto?> fieldExplorerQuery)
        {
            return new Card(
                Layout.Vertical().Gap(2)
                | Text.H3("Огляд сигналу")
                | Text.Muted("Логіка: датасет - короткий огляд тут - структура - «Запустити порівняння».")
                | (Layout.Horizontal().Gap(4)
                    | BuildExplorerHalfPanel("Симуляція (модель)", simulationExplorerQuery)
                    | BuildExplorerHalfPanel("Поле (вимір)", fieldExplorerQuery))
                | BuildCrossDatasetResolutionHint(simulationExplorerQuery, fieldExplorerQuery));
        }

        private static object BuildCrossDatasetResolutionHint(
            QueryResult<ClientServices.DatasetSignalOverviewDto?> simulationExplorerQuery,
            QueryResult<ClientServices.DatasetSignalOverviewDto?> fieldExplorerQuery)
        {
            if (simulationExplorerQuery.Loading
                || fieldExplorerQuery.Loading
                || simulationExplorerQuery.Error is not null
                || fieldExplorerQuery.Error is not null)
            {
                return new Fragment();
            }

            var sim = simulationExplorerQuery.Value;
            var field = fieldExplorerQuery.Value;
            if (sim is null || field is null || sim.SampleCount == 0 || field.SampleCount == 0)
            {
                return new Fragment();
            }

            var nSim = sim.SampleCount;
            var nField = field.SampleCount;
            var minN = Math.Max(Math.Min(nSim, nField), 1);
            var countSkew = Math.Max(nSim, nField) / minN;
            var ratioFieldPerSim = (decimal)nField / Math.Max(nSim, 1);

            if (countSkew >= 10m)
            {
                return Callout.Warning(
                    $"Сильно відрізняється кількість зразків ({nSim} симуляція vs {nField} поле, ≈ {countSkew:F0}×). "
                    + "Ймовірно різні CSV або експерименти — рушій переходить на парування за прогресом експерименту (нормалізований час), "
                    + "а не один-до-одного по рядках. "
                    + "Для узгоджених рядків і зрозумілих метрик: у «Середовищна симуляція» оберіть «Віддзеркалити поле» — той самий датасет поля, "
                    + "створіть нову симуляцію й оберіть обидві гілки під цей запис.");
            }

            var pairingNote = ratioFieldPerSim >= 8m
                ? "Поле густіше (≥8×): по одній співставленій вимірі на ряд симуляції; при екстремальній щільності застосовується упорядкований субдискрет за квантилями часу."
                : "Менше зразків на одній гілці: для кожного зразка симуляції підбирається один відповідник (див. документацію порівняння). ";

            return Callout.Info(
                $"Перевірка узгодження: симуляція {nSim} vs поле {nField} зразків (поле/сим ≈ {ratioFieldPerSim:F2}). "
                + pairingNote);
        }

        private static object BuildExplorerHalfPanel(string role, QueryResult<ClientServices.DatasetSignalOverviewDto?> query)
        {
            if (query.Loading)
            {
                return Layout.Vertical().Gap(1).Width(Size.Fraction(0.48f))
                       | Text.H4(role)
                       | Skeleton.Card();
            }

            if (query.Error is { } err)
            {
                return Layout.Vertical().Gap(1).Width(Size.Fraction(0.48f))
                       | Text.H4(role)
                       | Callout.Warning(err.Message);
            }

            var o = query.Value;
            if (o is null)
            {
                return Layout.Vertical().Gap(1).Width(Size.Fraction(0.48f))
                       | Text.H4(role)
                       | Text.Muted("Оберіть датасет у списку вище для статистик сигналу.");
            }

            if (o.SampleCount == 0)
            {
                return Layout.Vertical().Gap(1).Width(Size.Fraction(0.48f))
                       | Text.H4(role)
                       | Text.Block($"{o.Name} ({o.SourceSystem}) — акустичні зразки ще не імпортовано.")
                       | Text.Muted("Завантажте CSV у «Керування датасетами» та оновіть вид.");
            }

            var durationText = o.DurationSeconds <= 0.0001m && o.SampleCount > 1
                ? $"{o.SampleCount} зразків на спільних мітках часу."
                : $"{o.SampleCount} зразків за {FormatDurationHuman(o.DurationSeconds)}.";

            return Layout.Vertical().Gap(1).Width(Size.Fraction(0.48f))
                   | Text.H4(role)
                   | Text.Block(o.Name).Bold()
                   | Text.Block(durationText)
                   | Text.Block(
                       $"Діапазон частот: {FormatFrequencyRangeSummary(o.FrequencyMinHz, o.FrequencyMaxHz)} "
                       + $"({o.DistinctFrequencyBins} різних хвиль)")
                   | Text.Block($"Пікова амплітуда: {o.PeakAmplitudeDb:F2} дБ")
                   | Text.Block(
                       $"Шумова підкладка (≈10-й процентиль амплітуди): {o.NoiseFloorDb:F2} дБ, середній рівень: {o.MeanAmplitudeDb:F2} дБ")
                   | (o.MeanNoiseLevelDb is null
                       ? Text.Muted("Стовпчик рівню шуму відсутній або порожній.")
                       : Text.Block($"Шумові телеметрії у записах (сер.): {o.MeanNoiseLevelDb.Value:F2} дБ"));
        }

        private sealed class ComparisonResultsSection(
            ClientServices.ComparisonResultDto result,
            Action openChartsBlade,
            string simulationSelectionLabel,
            string fieldSelectionLabel,
            ClientServices.DatasetSignalOverviewDto? simulationOverview,
            ClientServices.DatasetSignalOverviewDto? fieldOverview) : ViewBase
        {
            public override object? Build()
            {
                var auth = UseService<IAuthService>();
                var userQuery = UseQuery(
                    key: (AuthViewHelper.UserQueryKey, "comparison-results"),
                    fetcher: async ct =>
                    {
                        if (auth.GetAuthSession()?.AuthToken is null)
                        {
                            return (UserInfo?)null;
                        }

                        return auth is AuthService authService
                            ? await authService.GetUserInfoAsync(ct)
                            : null;
                    });
                var access = AuthAccess.From(auth, userQuery.Value);
                var (mismatchSheetView, openMismatchSheet) = UseTrigger((IState<bool> isOpen) =>
                    isOpen.Value
                        ? new Sheet(
                            _ => isOpen.Set(false),
                            Layout.Vertical().Gap(3)
                            | BuildTemporalClustersBlock(result)
                            | BuildTopDifferencesBlock(result),
                            title: "Часові кластери та топ відмінностей",
                            description: "Пакети сплесків і рядки викидів цього запуску порівняння.")
                            .Width(Size.Fraction(2f / 3f))
                        : null);

                var (metricsSheetView, openMetricsSheet) = UseTrigger((IState<bool> isOpen) =>
                    isOpen.Value
                        ? new Sheet(
                            _ => isOpen.Set(false),
                            BuildMetricsSheetBody(result),
                            title: "Метрики",
                            description: "Залишки для спарованих симуляція–поле та як їх читати.")
                            .Width(Size.Fraction(2f / 3f))
                        : null);

                var recommendationsStack = Layout.Vertical().Gap(2);
                foreach (var rec in result.Recommendations)
                {
                    recommendationsStack |= BuildRecommendationCard(rec);
                }

                var timelineNote = result.TotalComparedPoints > 0 && result.TimelineNormalizationApplied
                    ? Text.Muted(
                        "Спаровування використало вирівнювання за прогресом експерименту: мітки кожного CSV зображені у u∈[0,1] від першого/останнього зразка, далі узгодження найближчими u між узгоджуваними хвилями (спільний UTC не обов’язковий). Для суворого трекінгу краще однакова шкала часу UTC/секунд.")
                    : (object)new Fragment();

                return Layout.Vertical().Gap(2)
                       | new Card(
                           Layout.Vertical().Gap(2)
                           | Text.H3("Короткий підсумок")
                           | Text.Block(result.TotalComparedPoints == 0
                               ? "Немає спарованих зразків після масштабування хвиль (10^n Гц), найближчого збігу UTC та парування за прогресом (нормалізована частка u). Перевірте узгоджувані хвилі та непусті інтервали; див. висновки нижче."
                               : result.SignificantDifferenceCount == 0
                                   ? "Модель узгоджується з полем добре для цього запуску (лише на спарованих точках)."
                                   : "Модель потребує налаштування на частину порівнюваних точок.")
                           | timelineNote
                           | Text.H4("Рекомендації для рішення")
                           | Text.Muted(
                               "Кожен висновок — гіпотеза правил з явними доказами, не «чорний ящик» ML. "
                               + "Впевненість — детермінований показник зрозумілості (0–1) із порогів на цьому запуску; це не ймовірність ML. "
                               + "Більше значення — більше незалежних сигналів сходяться на тій самій гіпотезі.")
                           | Text.H4("Висновки")
                           | (result.Recommendations.Length == 0
                               ? Text.Muted("Для цього запуску рекомендацій немає.")
                               : recommendationsStack))
                       | (Layout.Horizontal().Gap(2)
                           | new Button("Часові кластери та топ відмінностей")
                               .OnClick(_ => openMismatchSheet())
                           | new Button("Метрики")
                               .OnClick(_ => openMetricsSheet())
                           | new Button("Відкрити графіки")
                               .OnClick(_ => openChartsBlade())
                           | (access.CanWrite
                               ? new ComparisonPdfDownloadView(new ComparisonReportPdfInput(
                                   result,
                                   simulationSelectionLabel,
                                   fieldSelectionLabel,
                                   simulationOverview,
                                   fieldOverview))
                               : Callout.Info("Завантаження PDF доступне після входу (не гість).")))
                       | mismatchSheetView
                       | metricsSheetView;
            }
        }

        private static object BuildMetricsSheetBody(ClientServices.ComparisonResultDto result)
        {
            var hasPairs = result.TotalComparedPoints > 0;

            object ValueLine(string line) =>
                hasPairs ? Text.Block(line) : Text.Muted("— ще немає спарованих зразків у цьому запуску.");

            return Layout.Vertical().Gap(3)
                   | (hasPairs
                       ? Text.Muted(
                           "Значення лічаться лише на спарованих точках після узгодження та перевірки перекриття.")
                       : Callout.Warning(
                           "Порівнюваних точок: 0 — спочатку точний ряд, далі найближчі UTC із масштабом хвиль (обмеження перекошення), потім парування за прогресом (мін |u_сим−u_поле|). Числа з’являться за наявності пар."))
                   | Text.Block("MAE — середня абсолютна помилка").Bold()
                   | Text.Muted(
                       "Середнє абсолютного зазору дБ між симулятором та полем на кожній спарованій точці. Типова величина помилки "
                       + "без напряму; менш чутливо до викидів, ніж RMSE, але однакова вага всіх точок.")
                   | ValueLine($"MAE: {result.Mae:F3} дБ")
                   | Text.Block("RMSE — корінь середньоквадратичної помилки").Bold()
                   | Text.Muted(
                       "Корінь із середнього квадрату залишків у дБ. Сильніше підсилює великі розбіжності — сплески сильніше «тягнуть» RMSE за MAE.")
                   | ValueLine($"RMSE: {result.Rmse:F3} дБ")
                   | Text.Block("MRE — середня відносна помилка").Bold()
                   | Text.Muted(
                       "Середнє абсолютної відносної різниці до спостереженого SPL поля, у відсотках для цього прототипу. "
                       + "Читай разом із масштабом амплітуди: дуже низьке SNR механічно роздуває відсотки.")
                   | ValueLine($"MRE: {result.MeanRelativeErrorPercent:F2}%")
                   | Text.Block("P95 — 95-й процентиль абсолютної помилки").Bold()
                   | Text.Muted(
                       "Такий залишок у дБ, що приблизно 95% спарованих невідповідностей нижчі — узагальнення по «хвосту» поруч із MAE/RMSE.")
                   | ValueLine($"P95 абсолютної помилки: {result.P95AbsoluteError:F3} дБ")
                   | Text.Block("Суттєво відмінні точки").Bold()
                   | Text.Muted(
                       "Кількість спарованих зразків, класифікованих як матеріально інші проти загальної кількості. "
                       + "Інтервали задаються порогами відносної амплітуди — див. рівні серйозності нижче.")
                   | ValueLine(
                       $"Суттєво відмінних: {result.SignificantDifferenceCount} / {result.TotalComparedPoints} спарованих зразків "
                       + "(за рівнем відносної помилки амплітуди)")
                   | Text.H4("Інтервали серйозності (відносна помилка амплітуди)")
                   | Text.Muted(
                       "НИЗЬКА < 2%; ПОМІРНА 2–5%; ВИСОКА 5–10%; КРИТИЧНА > 10%. "
                       + "Ці мітки визначають чисельник «суттєвих» і допомагають пріоритизувати проблемні місця.");
        }

        private static object BuildTemporalClustersBlock(ClientServices.ComparisonResultDto result) =>
            Layout.Vertical().Gap(1)
            | Text.H4("Часові кластери (топ розбіжностей)")
            | (result.TemporalClusters.Length == 0
                ? Text.Muted("Кластери з’являються коли група зразків має ту саму хвилю й мітки часу з різницею ≈до 75 мс.")
                : new List(result.TemporalClusters.Select(c =>
                    new ListItem(
                        $"Кластер №{c.Ordinal}: {c.TimeStart:HH:mm:ss.fff}–{c.TimeEnd:HH:mm:ss.fff} | {c.FrequencyBand} Гц | "
                        + $"n={c.PointCount}, серед. відносн. помилка {c.MeanRelativeErrorPercent:F1}%"))));

        private static object BuildTopDifferencesBlock(ClientServices.ComparisonResultDto result) =>
            Layout.Vertical().Gap(1)
            | Text.H4("Найбільші відмінності")
            | (result.TopDifferences.Length == 0
                ? Text.Muted("У списку Top-N немає рядів-викидів.")
                : new List(result.TopDifferences.Select(x =>
                    new ListItem(
                        $"{x.Timestamp:HH:mm:ss.fff} | {x.FrequencyBand} Гц | відн.={x.RelativeErrorPercent:F1}% | {RecommendationCopyUa.SeverityLabel(x.Severity)}"))));

        private static object BuildRecommendationCard(ClientServices.RecommendationDto rec)
        {
            var title = $"{RecommendationCopyUa.CategoryTitle(rec.Category)} — {RecommendationCopyUa.ReasonTitle(rec.ReasonCode)}";
            return new Card(
                Layout.Vertical().Gap(1)
                       | Text.H4(title)
                       | Text.Block($"Впевненість: {rec.Confidence:P0}")
                       | Text.Muted($"Метод: {RecommendationCopyUa.InferenceMethodLabel(rec.InferenceMethod)}")
                       | Text.Block(RecommendationCopyUa.Explanation(rec))
                       | Text.H4("Докази")
                       | (rec.EvidenceSignals.Length == 0
                           ? Text.Muted("Структурованих доказів немає.")
                           : new List(rec.EvidenceSignals.Select(s =>
                               new ListItem(RecommendationCopyUa.EvidenceLine(s)))))
                       | Text.Block($"Запропонована дія: {RecommendationCopyUa.SuggestedAction(rec)}"));
        }

    }

    private sealed class ChartsBlade(ClientServices.ComparisonResultDto result) : ViewBase
    {
        public override object? Build()
        {
            var metricRows = new[]
            {
                new { Metric = "MAE", Value = (double)result.Mae },
                new { Metric = "RMSE", Value = (double)result.Rmse },
                new { Metric = "MRE", Value = (double)result.MeanRelativeErrorPercent },
                new { Metric = "P95", Value = (double)result.P95AbsoluteError }
            };

            var overlayBand = result.OverlaySeries.FirstOrDefault()?.FrequencyBand ?? 0m;
            var overlayRows = result.OverlaySeries
                .Select(x => new
                {
                    Time = x.Timestamp.ToString("HH:mm"),
                    Sim = (double)x.SimulationDb,
                    Field = (double)x.FieldDb
                })
                .ToArray();

            var heatmapRows = result.MismatchHeatmap
                .OrderByDescending(x => x.MaxRelativeErrorPercent)
                .Take(32)
                .Select(x => new
                {
                    Cell = $"{x.FrequencyBand}Hz @ {x.TimeBucket}",
                    Error = (double)x.MaxRelativeErrorPercent
                })
                .ToArray();

            return Layout.Vertical().Gap(2)
                   | Text.H3("Графіки")
                   | (result.TotalComparedPoints == 0
                       ? Callout.Warning(
                           "Спарованих зразків немає — графіки нижче порожні або нульові й не описують якість моделі.")
                       : new Fragment())
                   | new Card(
                       Layout.Vertical()
                       | Text.Block("Метрики порівняння")
                       | metricRows.ToBarChart(
                           e => e.Metric,
                           [e => e.Sum(v => v.Value)],
                           BarChartStyles.Default)
                       | Text.Muted("Стовпчикова підсумок глобальних показників якості для поточного запуску."))
                   | new Card(
                       Layout.Vertical()
                       | Text.Block(
                           $"Накладання: симуляція vs поле (основна контрольна хвиля ~ {overlayBand} Гц)")
                       | (overlayRows.Length == 0
                           ? Text.Muted("Немає точок для накладання в цьому запуску.")
                           : overlayRows.ToLineChart(
                               e => e.Time,
                               [e => e.Sum(v => v.Sim), e => e.Sum(v => v.Field)],
                               LineChartStyles.Dashboard))
                       | Text.Muted("Дві криві амплітуд по головній хвилі показують де модель збігається з записом."))
                   | new Card(
                       Layout.Vertical()
                       | Text.Block("Теплова карта невідповідностей (хвилина × частота, макс. відносна помилка)")
                       | (heatmapRows.Length == 0
                           ? Text.Muted("Агрегатів теплокарти немає.")
                           : heatmapRows.ToBarChart(
                               e => e.Cell,
                               [e => e.Sum(v => v.Error)],
                               BarChartStyles.Default))
                       | Text.Muted("Висота стовпчиків імітує «гарячі» комірки частота–час."));
        }
    }

    private static bool IsSimulationType(string type) =>
        string.Equals(type, "simulation", StringComparison.OrdinalIgnoreCase);

    private static bool IsFieldType(string type) =>
        string.Equals(type, "field", StringComparison.OrdinalIgnoreCase);

    private static string ToOption(ClientServices.DatasetDto dataset) =>
        $"{dataset.Name} | {dataset.SourceSystem} | {dataset.SampleCount} зразків [{dataset.Id}]";

    private static Guid TryParseDatasetId(string value)
    {
        try
        {
            return ParseDatasetId(value);
        }
        catch
        {
            return Guid.Empty;
        }
    }

    private static Guid ParseDatasetId(string value)
    {
        var openIndex = value.LastIndexOf('[');
        var closeIndex = value.LastIndexOf(']');
        if (openIndex < 0 || closeIndex <= openIndex)
        {
            throw new InvalidOperationException("Невірне значення датасету.");
        }

        return Guid.Parse(value.Substring(openIndex + 1, closeIndex - openIndex - 1));
    }

    private static string FormatFrequencyHz(decimal hz) =>
        hz >= 1000m ? $"{hz / 1000m:N1} кГц" : $"{hz:N0} Гц";

    private static string FormatFrequencyRangeSummary(decimal minHz, decimal maxHz) =>
        $"{FormatFrequencyHz(minHz)} – {FormatFrequencyHz(maxHz)}";

    private static string FormatDurationHuman(decimal seconds)
    {
        if (seconds >= 7200)
        {
            return $"{seconds / 3600m:N1} год";
        }

        if (seconds >= 120)
        {
            return $"{seconds / 60m:N1} хв";
        }

        return $"{seconds:N3} с";
    }
}
