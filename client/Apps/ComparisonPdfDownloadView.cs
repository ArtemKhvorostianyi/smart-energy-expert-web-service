using Ivy;
using SmartEnergyExpert.Client.Reporting;
using SmartEnergyExpert.Client.Services;

namespace SmartEnergyExpert.Client.Apps;

/// <summary>Кнопка завантаження PDF-звіту для одного <see cref="ComparisonResultDto"/>.</summary>
public sealed class ComparisonPdfDownloadView(ComparisonResultDto result) : ViewBase
{
    public override object? Build()
    {
        var downloadUrl = UseDownload(
            factory: () => ComparisonReportPdf.Build(result),
            mimeType: "application/pdf",
            fileName: $"porivnyannya-hydro-{result.ComparisonRunId:N}.pdf");

        return downloadUrl.Value is not null
            ? new Button("Зберегти PDF").Icon(Icons.Download).Url(downloadUrl.Value)
            : Text.Muted("Підготовка PDF…");
    }
}
