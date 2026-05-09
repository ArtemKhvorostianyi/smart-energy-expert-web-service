using ClientServices = SmartEnergyExpert.Client.Services;

namespace SmartEnergyExpert.Client.Apps;

[App(
    icon: Icons.Database,
    title: "Datasets management",
    group: ["Datasets"],
    searchHints: ["datasets", "csv", "import", "upload", "delete", "management", "samples"])]
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
            datasetsList = Text.Muted("No datasets in the workspace yet — import CSV above or seed the API.");
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
                var title = string.IsNullOrWhiteSpace(dataset.Name) ? "(unnamed dataset)" : dataset.Name;
                var id = dataset.Id;
                stack |= new Card(
                    Layout.Vertical().Gap(1)
                    | Text.H3(title)
                    | new Button("Delete dataset")
                        .Disabled(deleteBusy.Value)
                        .OnClick(async () =>
                        {
                            deleteBusy.Set(true);
                            try
                            {
                                await api.DeleteDatasetAsync(id);
                                refreshTick.Set(refreshTick.Value + 1);
                                status.Set($"Deleted '{title}'.");
                            }
                            catch (Exception ex)
                            {
                                status.Set($"Delete failed: {ex.Message}");
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
               | Text.H2("Datasets management")
               | Text.Muted("CSV import creates a new dataset named from the file. Below lists every dataset from the API (seeded, synthetic, imported).")

               | (datasetsQuery.Error is { } err ? Callout.Warning(err.Message) : new Fragment())
               | (datasetsQuery.Loading ? Callout.Info("Loading datasets…") : new Fragment())

               | new Card(
                   Layout.Vertical().Gap(2)
                   | Text.H3("Import CSV")
                   | Text.Muted("UTF-8: timestamp plus six numeric columns (same header as data/arlut_field.csv). Each import creates a fresh dataset.")

                   | (Layout.Vertical().Gap(1)
                       | Text.Block("Simulation").Bold()
                       | Text.Muted("Type simulation — use in Hydroacoustic Comparison as the model branch.")
                       | simCsvUpload
                           .ToFileInput(simUpload)
                           .Placeholder("Choose simulation .csv …")
                       | new Button("Import simulation CSV")
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
                       | Text.Block("Field experiments").Bold()
                       | Text.Muted("Type field — measurement branch for comparison.")
                       | fieldCsvUpload
                           .ToFileInput(fieldUpload)
                           .Placeholder("Choose field .csv …")
                       | new Button("Import field CSV")
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

               | Text.H3("Datasets")
               | Text.Muted("Each card lists one dataset name. Delete removes it and linked comparison runs.")
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
            status.Set("Choose a CSV file first.");
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

            status.Set(n == 0
                ? $"Created '{created.Name}' but imported 0 rows — check CSV format."
                : $"Imported {n} row(s) into '{created.Name}' ({datasetType}).");
        }
        catch (Exception ex)
        {
            status.Set($"Import failed: {ex.Message}");
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
