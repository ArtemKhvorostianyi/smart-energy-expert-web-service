using QuestPDF.Fluent;
using SmartEnergyExpert.Client.Services;

namespace SmartEnergyExpert.Client.Reporting;

/// <summary>PDF-звіт з результатів одного запуску порівняння (українські підписи).</summary>
public static class ComparisonReportPdf
{
    public static byte[] Build(ComparisonResultDto r)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(QuestPDF.Helpers.PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));
                page.PageColor(QuestPDF.Helpers.Colors.White);

                page.Header().PaddingBottom(8).BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Medium)
                    .Text("Гідроакустичне порівняння — звіт")
                    .FontSize(16).SemiBold();

                page.Content().Column(main =>
                {
                    main.Spacing(10);

                    main.Item().Text($"Ідентифікатор запуску: {r.ComparisonRunId}");
                    main.Item().Text($"Згенеровано (UTC): {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}");
                    if (r.TimelineNormalizationApplied)
                    {
                        main.Item().Text(
                            "Спаровування: застосовано вирівнювання за прогресом експерименту (нормалізована частка часу u).");
                    }

                    main.Item().LineHorizontal(0.5f).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten2);

                    main.Item().Text("Метрики").SemiBold().FontSize(12);
                    main.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(3);
                        });

                        void Row(string label, string value)
                        {
                            table.Cell().Element(c => c.PaddingVertical(2)).Text(label);
                            table.Cell().Element(c => c.PaddingVertical(2)).Text(value);
                        }

                        Row("MAE (дБ)", r.TotalComparedPoints > 0 ? $"{r.Mae:F3}" : "—");
                        Row("RMSE (дБ)", r.TotalComparedPoints > 0 ? $"{r.Rmse:F3}" : "—");
                        Row("MRE (%)", r.TotalComparedPoints > 0 ? $"{r.MeanRelativeErrorPercent:F2}" : "—");
                        Row("P95 абсолютної помилки (дБ)", r.TotalComparedPoints > 0 ? $"{r.P95AbsoluteError:F3}" : "—");
                        Row("Спарованих точок", $"{r.TotalComparedPoints}");
                        Row("Суттєво відмінних", $"{r.SignificantDifferenceCount}");
                    });

                    main.Item().LineHorizontal(0.5f).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten2);

                    main.Item().Text("Найбільші відмінності (Top-N)").SemiBold().FontSize(12);
                    if (r.TopDifferences.Length == 0)
                    {
                        main.Item().Text("Немає рядів у списку.");
                    }
                    else
                    {
                        main.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(2);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                            });
                            table.Header(header =>
                            {
                                header.Cell().Text("Час (UTC)").SemiBold();
                                header.Cell().Text("f, Гц").SemiBold();
                                header.Cell().Text("Сим., дБ").SemiBold();
                                header.Cell().Text("Поле, дБ").SemiBold();
                                header.Cell().Text("Відн., %").SemiBold();
                                header.Cell().Text("Рівень").SemiBold();
                            });
                            foreach (var d in r.TopDifferences.Take(50))
                            {
                                table.Cell().Text(d.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                                table.Cell().Text($"{d.FrequencyBand}");
                                table.Cell().Text($"{d.SimulationValue:F2}");
                                table.Cell().Text($"{d.FieldValue:F2}");
                                table.Cell().Text($"{d.RelativeErrorPercent:F1}");
                                table.Cell().Text(RecommendationCopyUa.SeverityLabel(d.Severity));
                            }
                        });
                    }

                    main.Item().LineHorizontal(0.5f).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten2);

                    main.Item().Text("Часові кластери").SemiBold().FontSize(12);
                    if (r.TemporalClusters.Length == 0)
                    {
                        main.Item().Text("Кластерів немає.");
                    }
                    else
                    {
                        main.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.ConstantColumn(36);
                                cols.RelativeColumn(2);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                            });
                            table.Header(h =>
                            {
                                h.Cell().Text("№").SemiBold();
                                h.Cell().Text("Інтервал часу").SemiBold();
                                h.Cell().Text("f, Гц").SemiBold();
                                h.Cell().Text("n").SemiBold();
                                h.Cell().Text("Сер. відн. %").SemiBold();
                            });
                            foreach (var c in r.TemporalClusters.Take(30))
                            {
                                table.Cell().Text($"{c.Ordinal}");
                                table.Cell().Text($"{c.TimeStart:HH:mm:ss.fff} — {c.TimeEnd:HH:mm:ss.fff}");
                                table.Cell().Text($"{c.FrequencyBand}");
                                table.Cell().Text($"{c.PointCount}");
                                table.Cell().Text($"{c.MeanRelativeErrorPercent:F1}");
                            }
                        });
                    }

                    main.Item().LineHorizontal(0.5f).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten2);

                    main.Item().Text("Рекомендації").SemiBold().FontSize(12);
                    if (r.Recommendations.Length == 0)
                    {
                        main.Item().Text("Рекомендацій немає.");
                    }
                    else
                    {
                        foreach (var rec in r.Recommendations)
                        {
                            main.Item().Border(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(8).Column(block =>
                            {
                                block.Spacing(4);
                                block.Item().Text(
                                        $"{RecommendationCopyUa.CategoryTitle(rec.Category)} — {RecommendationCopyUa.ReasonTitle(rec.ReasonCode)}")
                                    .SemiBold();
                                block.Item().Text(
                                    $"Впевненість: {RecConfidenceText(rec)}, метод: {RecommendationCopyUa.InferenceMethodLabel(rec.InferenceMethod)}");
                                block.Item().Text(RecommendationCopyUa.Explanation(rec));
                                block.Item().Text("Докази:").SemiBold();
                                if (rec.EvidenceSignals.Length == 0)
                                {
                                    block.Item().Text("—");
                                }
                                else
                                {
                                    foreach (var ev in rec.EvidenceSignals)
                                    {
                                        block.Item().Text("• " + RecommendationCopyUa.EvidenceLine(ev));
                                    }
                                }

                                block.Item().Text("Запропонована дія:").SemiBold();
                                block.Item().Text(RecommendationCopyUa.SuggestedAction(rec));
                            });
                        }
                    }
                });

                page.Footer()
                    .AlignCenter()
                    .DefaultTextStyle(x => x.FontSize(9).FontColor(QuestPDF.Helpers.Colors.Grey.Medium))
                    .Text(t =>
                    {
                        t.Span("Сторінка ");
                        t.CurrentPageNumber();
                        t.Span(" / ");
                        t.TotalPages();
                    });
            });
        }).GeneratePdf();
    }

    private static string RecConfidenceText(RecommendationDto rec) =>
        rec.Confidence.ToString("P0", System.Globalization.CultureInfo.GetCultureInfo("uk-UA"));
}
