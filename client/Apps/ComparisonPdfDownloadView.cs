using Ivy;
using SmartEnergyExpert.Client.Reporting;
using SmartEnergyExpert.Client.Services;

namespace SmartEnergyExpert.Client.Apps;

/// <summary>Кнопка завантаження PDF-звіту для одного запуску порівняння з контекстом датасетів.</summary>
public sealed class ComparisonPdfDownloadView(ComparisonReportPdfInput pdfInput) : ViewBase
{
    public override object? Build()
    {
        var downloadUrl = UseDownload(
            factory: () => ComparisonReportPdf.Build(pdfInput),
            mimeType: "application/pdf",
            fileName: $"porivnyannya-hydro-{pdfInput.Result.ComparisonRunId:N}.pdf");

        return downloadUrl.Value is not null
            ? new Button("Зберегти PDF").Icon(Icons.Download).Url(downloadUrl.Value)
            : Text.Muted("Підготовка PDF…");
    }
}
