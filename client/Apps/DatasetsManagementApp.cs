using ClientServices = SmartEnergyExpert.Client.Services;

namespace SmartEnergyExpert.Client.Apps;

[App(
    icon: Icons.Database,
    title: "Datasets management",
    group: ["Datasets"],
    searchHints: ["datasets", "csv", "import", "upload", "delete", "create", "management", "samples"])]
public sealed class DatasetsManagementApp : ViewBase
{
    private static readonly string[] DatasetTypeOptions = ["simulation", "field"];

    public override object? Build()
    {
        var api = UseService<ClientServices.IApiClient>();
        var refreshTick = UseState(0);
        var status = UseState("");

        var newName = UseState("");
        var newType = UseState("field");
        var newSourceSystem = UseState("csv-import");
        var newVersion = UseState("v1");

        var selectedDatasetOption = UseState("");
        var csvFileUpload = UseState<FileUpload<byte[]>?>();

        var busyCreate = UseState(false);
        var busyImportFile = UseState(false);
        var busyDelete = UseState(false);

        var datasetsQuery = UseQuery(
            key: (nameof(DatasetsManagementApp), refreshTick.Value),
            fetcher: async ct => await api.GetDatasetsAsync(ct));

        var csvUploadCore = UseUpload(MemoryStreamUploadHandler.Create(csvFileUpload));
        var csvUpload = csvUploadCore
            .Accept("text/csv,.csv,text/plain")
            .MaxFileSize(FileSize.FromMegabytes(128));

        var datasets = datasetsQuery.Value ?? [];
        var datasetOptions = datasets.Select(ToOption).ToArray();
        var canPickDataset = datasetOptions.Length > 0;
        var csvBytesReady = csvFileUpload.Value?.Content is byte[] csvBuf && csvBuf.Length > 0;

        return Layout.Vertical().Gap(2)
               | Text.H2("Datasets management")
               | Text.P(
                   "Create empty datasets, import hydroacoustic samples from a CSV file via the picker below, "
                   + "and remove datasets together with comparisons that referenced them.")

               | (datasetsQuery.Error is { } err ? Callout.Warning(err.Message) : new Fragment())
               | (datasetsQuery.Loading ? Callout.Info("Loading datasets…") : new Fragment())

               | new Card(
                   Layout.Vertical().Gap(1)
                   | Text.H3("Create dataset")
                   | newName.ToTextInput().Placeholder("Name")
                   | Text.Muted("Type: simulation (model output) vs field (measurements). Used by the comparison workflow.")
                   | newType.ToSelectInput(DatasetTypeOptions)
                   | newSourceSystem.ToTextInput().Placeholder("Source system (e.g. csv-import)")
                   | newVersion.ToTextInput().Placeholder("Version")
                   | new Button("Create dataset").Primary().Disabled(busyCreate.Value).OnClick(async () =>
                   {
                       if (string.IsNullOrWhiteSpace(newName.Value))
                       {
                           status.Set("Enter a dataset name.");
                           return;
                       }

                       busyCreate.Set(true);
                       try
                       {
                           var created = await api.CreateDatasetAsync(new ClientServices.CreateDatasetRequestDto
                           {
                               Name = newName.Value.Trim(),
                               Type = string.IsNullOrWhiteSpace(newType.Value) ? "field" : newType.Value.Trim(),
                               SourceSystem = string.IsNullOrWhiteSpace(newSourceSystem.Value)
                                   ? "csv-import"
                                   : newSourceSystem.Value.Trim(),
                               Version = string.IsNullOrWhiteSpace(newVersion.Value) ? "v1" : newVersion.Value.Trim()
                           });
                           refreshTick.Set(refreshTick.Value + 1);
                           selectedDatasetOption.Set(ToOption(created));
                           status.Set($"Created '{created.Name}' ({created.Type}), id={created.Id}. Import CSV next.");
                       }
                       catch (Exception ex)
                       {
                           status.Set($"Create failed: {ex.Message}");
                       }
                       finally
                       {
                           busyCreate.Set(false);
                       }
                   }))

               | new Card(
                   Layout.Vertical().Gap(1)
                   | Text.H3("Target dataset for import / delete")
                   | Text.Muted("Which dataset receives the CSV.")
                   | (canPickDataset
                       ? selectedDatasetOption.ToSelectInput(datasetOptions)
                       : Text.Muted("No datasets yet — create one first.")))

               | new Card(
                   Layout.Vertical().Gap(1)
                   | Text.Muted("UTF-8 CSV: timestamp plus six numeric columns (same header as data/arlut_field.csv).")
                   | csvFileUpload
                       .ToFileInput(csvUpload)
                       .Variant(FileInputVariant.Default)
                       .Placeholder("Choose .csv …")
                   | new Button("Import")
                       .Disabled(!canPickDataset || busyImportFile.Value || !csvBytesReady)
                       .Primary()
                       .OnClick(async () =>
                       {
                           var id = TryParseDatasetId(selectedDatasetOption.Value);
                           if (id == Guid.Empty)
                           {
                               status.Set("Pick a dataset in Target dataset above.");
                               return;
                           }

                           if (csvFileUpload.Value?.Content is not byte[] bytes || bytes.Length == 0)
                           {
                               status.Set("Choose a CSV file first.");
                               return;
                           }

                           busyImportFile.Set(true);
                           try
                           {
                               var uf = csvFileUpload.Value!;
                               var payload = StripUtf8Bom(bytes);
                               var n = await api.ImportCsvFileMultipartAsync(id, payload, uf.FileName ?? "import.csv");
                               refreshTick.Set(refreshTick.Value + 1);
                               csvFileUpload.Set(null);

                               status.Set(n == 0
                                   ? "Import finished — 0 rows accepted (check column format)."
                                   : $"Imported {n} row(s).");
                           }
                           catch (Exception ex)
                           {
                               status.Set($"CSV import failed: {ex.Message}");
                           }
                           finally
                           {
                               busyImportFile.Set(false);
                           }
                       }))

               | new Card(
                   Layout.Vertical().Gap(1)
                   | Text.H3("Delete dataset")
                   | Text.Muted("Deletes dataset, samples, and comparisons that referenced it.")
                   | new Button("Delete selected dataset").Disabled(!canPickDataset || busyDelete.Value).OnClick(async () =>
                   {
                       var id = TryParseDatasetId(selectedDatasetOption.Value);
                       if (id == Guid.Empty)
                       {
                           status.Set("Pick a dataset in Target dataset above.");
                           return;
                       }

                       busyDelete.Set(true);
                       try
                       {
                           await api.DeleteDatasetAsync(id);
                           refreshTick.Set(refreshTick.Value + 1);
                           selectedDatasetOption.Set("");
                           status.Set($"Dataset {id} deleted.");
                       }
                       catch (Exception ex)
                       {
                           status.Set($"Delete failed: {ex.Message}");
                       }
                       finally
                       {
                           busyDelete.Set(false);
                       }
                   }))

               | (string.IsNullOrWhiteSpace(status.Value) ? new Fragment() : Callout.Info(status.Value));
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

    private static string ToOption(ClientServices.DatasetDto dataset) =>
        $"{dataset.Name} | {dataset.SourceSystem} | {dataset.SampleCount} samples [{dataset.Id}]";

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
            throw new InvalidOperationException("Invalid dataset value.");
        }

        return Guid.Parse(value.Substring(openIndex + 1, closeIndex - openIndex - 1));
    }
}
