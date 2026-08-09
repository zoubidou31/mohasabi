using System.Globalization;
using System.IO;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Factur.Infrastructure.Export;

/// <summary>Données tabulaires génériques pour un rapport exporté.</summary>
public sealed class ReportData
{
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public IReadOnlyList<string> Headers { get; init; } = new List<string>();
    public IReadOnlyList<IReadOnlyList<object?>> Rows { get; init; } = new List<IReadOnlyList<object?>>();
    public IReadOnlyList<decimal?> Totals { get; init; } = new List<decimal?>();
    public IReadOnlyList<(string Label, string Value)> Summary { get; init; } = new List<(string, string)>();
}

/// <summary>Génère les rapports en PDF.</summary>
public static class ReportPdfRenderer
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    private const string Accent = "#1A237E";

    public static byte[] Render(ReportData report, ExportDocument header)
    {
        var s = header.Strings;
        var accent = Accent;

        var document = Document.Create(d =>
        {
            d.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(26);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor("#1F2937"));

                page.Header().Column(h =>
                {
                    h.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text(header.Company.Name).FontSize(14).Bold().FontColor(accent);
                            if (!string.IsNullOrWhiteSpace(header.Company.NIF))
                            {
                                left.Item().PaddingTop(1).Text($"NIF : {header.Company.NIF}").FontSize(7.5f).FontColor("#64748B");
                            }
                        });
                        row.ConstantItem(210).AlignRight().Column(right =>
                        {
                            right.Item().Text(report.Title).FontSize(15).Bold().FontColor(accent);
                            if (!string.IsNullOrWhiteSpace(report.Subtitle))
                            {
                                right.Item().PaddingTop(1).AlignRight().Text(report.Subtitle).FontSize(9);
                            }
                        });
                    });

                    h.Item().PaddingTop(6).LineHorizontal(1.2f).LineColor(accent);
                });

                page.Content().Column(content =>
                {
                    if (report.Summary.Count > 0)
                    {
                        content.Item().PaddingTop(8).Row(sum =>
                        {
                            foreach (var (label, value) in report.Summary)
                            {
                                sum.RelativeItem().Padding(2).Column(cell =>
                                {
                                    cell.Item().Background("#F1F5F9").Padding(4).Border(0.5f).BorderColor("#E2E8F0").Column(box =>
                                    {
                                        box.Item().Text(label).FontSize(7).FontColor("#64748B");
                                        box.Item().Text(value).FontSize(10).SemiBold();
                                    });
                                });
                            }
                        });
                    }

                    content.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            for (var i = 0; i < report.Headers.Count; i++)
                            {
                                c.RelativeColumn();
                            }
                        });

                        table.Header(headerRow =>
                        {
                            foreach (var h in report.Headers)
                            {
                                headerRow.Cell().Element(x => x.Border(0.5f).BorderColor(accent).Background(accent).Padding(4))
                                    .Text(h).FontColor("#FFFFFF").SemiBold().FontSize(8);
                            }
                        });

                        foreach (var row in report.Rows)
                        {
                            foreach (var value in row)
                            {
                                table.Cell().Element(x => x.Border(0.5f).BorderColor("#E2E8F0").Padding(4))
                                    .Text(Format(value)).FontSize(8).FontColor("#334155");
                            }
                        }

                        if (report.Totals.Count > 0)
                        {
                            table.Cell().Element(x => x.Border(0.5f).BorderColor(accent).Background("#E8EAF6").Padding(4))
                                .Text(s.TotalLabel).FontSize(8).SemiBold();
                            for (var i = 1; i < report.Totals.Count; i++)
                            {
                                var t = report.Totals[i];
                                table.Cell().Element(x => x.Border(0.5f).BorderColor(accent).Background("#E8EAF6").Padding(4))
                                    .Text(t.HasValue ? Money(t.Value) : string.Empty).FontSize(8).SemiBold();
                            }
                        }
                    });
                });

                page.Footer().DefaultTextStyle(x => x.FontSize(7)).AlignRight().Text(t =>
                {
                    t.Span($"{s.Page} ").FontColor("#94A3B8");
                    t.CurrentPageNumber().FontColor("#94A3B8");
                    t.Span($" {s.Of} ").FontColor("#94A3B8");
                    t.TotalPages().FontColor("#94A3B8");
                });
            });
        });

        return document.GeneratePdf();
    }

    private static string Money(decimal v) => $"{v.ToString("N2", Fr)} DA";

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        decimal d => Money(d),
        DateTime dt => dt.ToString("dd/MM/yyyy", Fr),
        _ => value.ToString() ?? string.Empty,
    };
}

/// <summary>Génère les rapports en Excel.</summary>
public static class ReportExcelRenderer
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    private const string Accent = "#1A237E";
    private const string MoneyFormat = "#,##0.00 \"DA\"";

    public static byte[] Render(ReportData report, DocumentStrings strings, CompanyBlock company)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(report.Title.Length > 24 ? report.Title[..24] : report.Title);

        ws.Cell(1, 1).Value = FormulaSanitizer.Sanitize(report.Title);
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 15;
        ws.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml(Accent);

        if (!string.IsNullOrWhiteSpace(report.Subtitle))
        {
            ws.Cell(2, 1).Value = FormulaSanitizer.Sanitize(report.Subtitle);
            ws.Cell(2, 1).Style.Font.FontSize = 10;
        }

        ws.Cell(1, 6).Value = FormulaSanitizer.Sanitize(company.Name);
        ws.Cell(1, 6).Style.Font.Bold = true;
        ws.Cell(1, 6).Style.Font.FontSize = 10;

        var startRow = 3;
        if (report.Summary.Count > 0)
        {
            var col = 1;
            foreach (var (label, value) in report.Summary)
            {
                ws.Cell(startRow, col).Value = FormulaSanitizer.Sanitize(label);
                ws.Cell(startRow, col).Style.Font.FontSize = 8;
                ws.Cell(startRow, col).Style.Font.FontColor = XLColor.FromHtml("#64748B");
                ws.Cell(startRow + 1, col).Value = FormulaSanitizer.Sanitize(value);
                ws.Cell(startRow + 1, col).Style.Font.FontSize = 10;
                ws.Cell(startRow + 1, col).Style.Font.Bold = true;
                col++;
            }

            startRow += 3;
        }

        for (var i = 0; i < report.Headers.Count; i++)
        {
            var cell = ws.Cell(startRow, i + 1);
            cell.Value = FormulaSanitizer.Sanitize(report.Headers[i]);
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontSize = 9;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(Accent);
        }

        var row = startRow + 1;
        foreach (var line in report.Rows)
        {
            for (var i = 0; i < line.Count; i++)
            {
                var value = line[i];
                var cell = ws.Cell(row, i + 1);
                if (value is decimal d)
                {
                    cell.Value = (double)d;
                    cell.Style.NumberFormat.Format = MoneyFormat;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                }
                else if (value is DateTime dt)
                {
                    cell.Value = dt.ToString("dd/MM/yyyy", Fr);
                }
                else
                {
                    cell.Value = FormulaSanitizer.Sanitize(value?.ToString() ?? string.Empty);
                }

                cell.Style.Font.FontSize = 9;
            }

            row++;
        }

        if (report.Totals.Count > 0)
        {
            ws.Cell(row, 1).Value = FormulaSanitizer.Sanitize(strings.TotalLabel);
            ws.Cell(row, 1).Style.Font.Bold = true;
            for (var i = 1; i < report.Totals.Count; i++)
            {
                var t = report.Totals[i];
                var cell = ws.Cell(row, i + 1);
                if (t.HasValue)
                {
                    cell.Value = (double)t.Value;
                    cell.Style.NumberFormat.Format = MoneyFormat;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                }

                cell.Style.Font.Bold = true;
            }

            ws.Range(row, 1, row, report.Headers.Count).Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EAF6");
        }

        ws.SheetView.FreezeRows(startRow + 1);
        ws.Range(startRow, 1, Math.Max(row, startRow), report.Headers.Count).SetAutoFilter();
        for (var i = 0; i < report.Headers.Count; i++)
        {
            ws.Column(i + 1).AdjustToContents();
            if (ws.Column(i + 1).Width < 10)
            {
                ws.Column(i + 1).Width = 10;
            }
        }

        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
        ws.PageSetup.FitToPages(1, 0);
        ws.PageSetup.Margins.Top = 0.4;
        ws.PageSetup.Margins.Bottom = 0.4;
        ws.PageSetup.Margins.Left = 0.3;
        ws.PageSetup.Margins.Right = 0.3;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
