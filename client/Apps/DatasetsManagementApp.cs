using ClientServices = SmartEnergyExpert.Client.Services;

namespace SmartEnergyExpert.Client.Apps;

[App(
    icon: Icons.Database,
    title: "Керування датасетами",
    group: ["Сервіс"],
    order: 10,
    searchHints: ["датасети", "csv", "імпорт", "видалення", "зразки", "datasets"])]
public sealed class DatasetsManagementApp : ViewBase
{
    private const string CsvImportSource = "csv-import";

    public override object? Build()
    {
        var api = UseService<ClientServices.IApiClient>();
        var refreshTick = UseState(0);
        var status = UseState("");
        var deleteBusy = UseState(false);

        var simCsvUpload = UseState<FileUpload<byte[]>?>();
        var fieldCsvUpload = UseState<FileUpload<byte[]>?>();

        var busySimImport = UseState(false);
        var busyFieldImport = UseState(false);

        var simUploadCore = UseUpload(MemoryStreamUploadHandler.Create(simCsvUpload));
        var fieldUploadCore = UseUpload(MemoryStreamUploadHandler.Create(fieldCsvUpload));

        var datasetsQuery = UseQuery(
            key: (nameof(DatasetsManagementApp), refreshTick.Value),
            fetcher: async ct => await api.GetDatasetsAsync(ct));

        var simUpload = simUploadCore
            .Accept("text/csv,.csv,text/plain")
            .MaxFileSize(FileSize.FromMegabytes(128));

        var fieldUpload = fieldUploadCore
            .Accept("text/csv,.csv,text/plain")
            .MaxFileSize(FileSize.FromMegabytes(128));

        var datasets = datasetsQuery.Value ?? [];
        var datasetsSorted = datasets
            .OrderByDescending(d => d.SampleCount)
            .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var simBytesReady = simCsvUpload.Value?.Content is byte[] sb && sb.Length > 0;
        var fieldBytesReady = fieldCsvUpload.Value?.Content is byte[] fb && fb.Length > 0;

        object datasetsList;
        if (datasetsSorted.Length == 0 && !datasetsQuery.Loading && datasetsQuery.Error is null)
        {
            datasetsList = Text.Muted(
                "Ще немає датасетів — імпортуйте CSV вище або дочекайтесь ініціалізації БД при старті.");
        }
        else if (datasetsSorted.Length == 0)
        {
            datasetsList = new Fragment();
        }
        else
        {
            var stack = Layout.Vertical().Gap(1);
            foreach (var dataset in datasetsSorted)
            {
                var title = string.IsNullOrWhiteSpace(dataset.Name) ? "(датасет без назви)" : dataset.Name;
                var id = dataset.Id;
                stack |= new Card(
                    Layout.Vertical().Gap(1)
                    | Text.H3(title)
                    | new Button("Видалити датасет")
                        .Disabled(deleteBusy.Value)
                        .OnClick(async () =>
                        {
                            deleteBusy.Set(true);
                            try
                            {
                                await api.DeleteDatasetAsync(id);
                                refreshTick.Set(refreshTick.Value + 1);
                                status.Set($"Видалено «{title}».");
                            }
                            catch (Exception ex)
                            {
                                status.Set($"Не вдалося видалити: {ex.Message}");
                            }
                            finally
                            {
                                deleteBusy.Set(false);
                            }
                        }));
            }

            datasetsList = stack;
        }

        return Layout.Vertical().Gap(2)
               | Text.H2("Керування датасетами")
               | Text.Muted(
                   "Імпорт CSV створює новий датасет з іменем файлу. Нижче — усі датасети з PostgreSQL (засіяні, синтетичні, імпортовані).")

               | (datasetsQuery.Error is { } err ? Callout.Warning(err.Message) : new Fragment())
               | (datasetsQuery.Loading ? Callout.Info("Завантаження датасетів…") : new Fragment())

               | new Card(
                   Layout.Vertical().Gap(2)
                   | Text.H3("Імпорт CSV")
                   | Text.Muted(
                       "UTF-8: мітка часу та шість числових стовпців (той самий заголовок, що в data/arlut_field.csv). Кожен імпорт — новий датасет.")

                   | (Layout.Vertical().Gap(1)
                       | Text.Block("Симуляція").Bold()
                       | Text.Muted(
                           "Тип simulation — гілка моделі в «Гідроакустичному порівнянні».")
                       | simCsvUpload
                           .ToFileInput(simUpload)
                           .Placeholder("Оберіть .csv симуляції…")
                       | new Button("Імпортувати CSV симуляції")
                           .Primary()
                           .Disabled(busySimImport.Value || !simBytesReady)
                           .OnClick(async () => await ImportCsvBranchAsync(
                               api,
                               simCsvUpload,
                               "simulation",
                               busySimImport,
                               refreshTick,
                               status,
                               () => simCsvUpload.Set(null))))

                   | (Layout.Vertical().Gap(1)
                       | Text.Block("Польові експерименти").Bold()
                       | Text.Muted("Тип field — гілка вимірів для порівняння.")
                       | fieldCsvUpload
                           .ToFileInput(fieldUpload)
                           .Placeholder("Оберіть польовий .csv…")
                       | new Button("Імпортувати польовий CSV")
                           .Primary()
                           .Disabled(busyFieldImport.Value || !fieldBytesReady)
                           .OnClick(async () => await ImportCsvBranchAsync(
                               api,
                               fieldCsvUpload,
                               "field",
                               busyFieldImport,
                               refreshTick,
                               status,
                               () => fieldCsvUpload.Set(null)))))

               | Text.H3("Датасети")
               | Text.Muted(
                   "Кожна картка — один датасет. Видалення прибирає його та пов’язані запуски порівняння.")
               | datasetsList

               | (string.IsNullOrWhiteSpace(status.Value) ? new Fragment() : Callout.Info(status.Value));
    }

    private static async Task ImportCsvBranchAsync(
        ClientServices.IApiClient api,
        IState<FileUpload<byte[]>?> fileState,
        string datasetType,
        IState<bool> busy,
        IState<int> refreshTick,
        IState<string> status,
        Action clearFile)
    {
        var upload = fileState.Value;
        if (upload?.Content is not byte[] bytes || bytes.Length == 0)
        {
            status.Set("Спочатку оберіть CSV-файл.");
            return;
        }

        var rawName = string.IsNullOrWhiteSpace(upload.FileName) ? "import.csv" : upload.FileName.Trim();
        var datasetName = Path.GetFileNameWithoutExtension(rawName);
        if (string.IsNullOrWhiteSpace(datasetName))
        {
            datasetName = "import";
        }

        busy.Set(true);
        try
        {
            var created = await api.CreateDatasetAsync(new ClientServices.CreateDatasetRequestDto
            {
                Name = datasetName,
                Type = datasetType,
                SourceSystem = CsvImportSource,
                Version = "v1"
            });

            var payload = StripUtf8Bom(bytes);
            var n = await api.ImportCsvFileMultipartAsync(created.Id, payload, Path.GetFileName(rawName));
            refreshTick.Set(refreshTick.Value + 1);
            clearFile();

            var typeUa = string.Equals(datasetType, "simulation", StringComparison.OrdinalIgnoreCase)
                ? "симуляція"
                : "поле";
            status.Set(n == 0
                ? $"Створено «{created.Name}», але імпортовано 0 рядків — перевірте формат CSV."
                : $"Імпортовано {n} ряд. у «{created.Name}» ({typeUa}).");
        }
        catch (Exception ex)
        {
            status.Set($"Помилка імпорту: {ex.Message}");
        }
        finally
        {
            busy.Set(false);
        }
    }

    private static byte[] StripUtf8Bom(byte[] raw)
    {
        if (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF)
        {
            var copy = new byte[raw.Length - 3];
            Buffer.BlockCopy(raw, 3, copy, 0, copy.Length);
            return copy;
        }

        return raw;
    }
}
