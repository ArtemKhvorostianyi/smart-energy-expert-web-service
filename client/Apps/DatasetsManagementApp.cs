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
        var csvPaste = UseState("");
        var serverCsvPath = UseState("");

        var busyCreate = UseState(false);
        var busyImportPaste = UseState(false);
        var busyImportPath = UseState(false);
        var busyDelete = UseState(false);

        var datasetsQuery = UseQuery(
            key: (nameof(DatasetsManagementApp), refreshTick.Value),
            fetcher: async ct => await api.GetDatasetsAsync(ct));

        var datasets = datasetsQuery.Value ?? [];
        var datasetOptions = datasets.Select(ToOption).ToArray();
        var canPickDataset = datasetOptions.Length > 0;

        return Layout.Vertical().Gap(2)
               | Text.H2("Datasets management")
               | Text.P(
                   "Create empty datasets, import hydroacoustic samples from CSV (paste or server file path when the Ivy host can read files), "
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
                   | Text.H3("Import CSV samples")
                   | Text.Muted(
                       "Select a dataset below. Rows: timestamp, frequency_band, amplitude_db, depth_meters, range_meters, sound_speed?, noise_level_db? "
                       + "(header row starting with timestamp is skipped).")
                   | (canPickDataset ? selectedDatasetOption.ToSelectInput(datasetOptions) : Text.Muted("No datasets yet — create one first."))
                   | csvPaste.ToCodeInput().Placeholder("Paste full CSV contents here.")
                   | new Button("Import from pasted CSV")
                       .Disabled(!canPickDataset || busyImportPaste.Value)
                       .OnClick(async () =>
                       {
                           var id = TryParseDatasetId(selectedDatasetOption.Value);
                           if (id == Guid.Empty)
                           {
                               status.Set("Select a dataset.");
                               return;
                           }

                           if (string.IsNullOrWhiteSpace(csvPaste.Value))
                           {
                               status.Set("Paste CSV content first.");
                               return;
                           }

                           busyImportPaste.Set(true);
                           try
                           {
                               var n = await api.ImportCsvSamplesAsync(id, csvPaste.Value.Trim());
                               refreshTick.Set(refreshTick.Value + 1);
                               status.Set(n == 0
                                   ? "Import finished — 0 rows accepted (check column format)."
                                   : $"Imported {n} sample row(s).");
                           }
                           catch (Exception ex)
                           {
                               status.Set($"CSV import failed: {ex.Message}");
                           }
                           finally
                           {
                               busyImportPaste.Set(false);
                           }
                       })
                   | Text.Muted("If the Ivy process runs locally, you may import straight from disk on the machine hosting the web client:")
                   | serverCsvPath.ToTextInput().Placeholder("/absolute/path/to/samples.csv")
                   | new Button("Import from server file path").Disabled(!canPickDataset || busyImportPath.Value).OnClick(async () =>
                   {
                       var id = TryParseDatasetId(selectedDatasetOption.Value);
                       if (id == Guid.Empty)
                       {
                           status.Set("Select a dataset.");
                           return;
                       }

                       if (string.IsNullOrWhiteSpace(serverCsvPath.Value))
                       {
                           status.Set("Enter a path to the CSV file on the Ivy host.");
                           return;
                       }

                       busyImportPath.Set(true);
                       try
                       {
                           var n = await api.ImportCsvFileAsync(id, serverCsvPath.Value.Trim());
                           refreshTick.Set(refreshTick.Value + 1);
                           status.Set(n == 0
                               ? "Import finished — 0 rows accepted (check path / format)."
                               : $"Imported {n} sample row(s) from file.");
                       }
                       catch (Exception ex)
                       {
                           status.Set($"CSV file import failed: {ex.Message}");
                       }
                       finally
                       {
                           busyImportPath.Set(false);
                       }
                   }))

               | new Card(
                   Layout.Vertical().Gap(1)
                   | Text.H3("Delete dataset")
                   | Text.Muted("Removes the dataset, all acoustic samples, and any comparison runs that used this dataset.")
                   | (canPickDataset ? selectedDatasetOption.ToSelectInput(datasetOptions) : Text.Muted("No datasets to delete."))
                   | new Button("Delete selected dataset").Disabled(!canPickDataset || busyDelete.Value).OnClick(async () =>
                   {
                       var id = TryParseDatasetId(selectedDatasetOption.Value);
                       if (id == Guid.Empty)
                       {
                           status.Set("Select a dataset.");
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
