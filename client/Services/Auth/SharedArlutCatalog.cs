namespace SmartEnergyExpert.Client.Services.Auth;

/// <summary>Спільна демо-пара ARLUT для гостя та зареєстрованих аналітиків (порівняння з першого входу).</summary>
public static class SharedArlutCatalog
{
    public const string SourceSystem = "arlut-shared";

    /// <summary>Мітка версії каталогу в БД; зміна — пересід пари.</summary>
    public const string CatalogVersion = "partA-01-stride2500";

    public const string SimulationName = "ARLUT 01 part A simulation (shared)";
    public const string FieldName = "ARLUT 01 part A field stride2500";

    public const string FieldCsvFileName = "ARLUT_01_partA_01_dataset_field_stride2500.csv";

    public const int MinExpectedSamples = 10_000;
}
