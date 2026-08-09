using System.Globalization;
using System.IO;
using ClosedXML.Excel;
using Factur.Application.DTOs;
using Factur.Domain.Enums;

namespace Factur.Infrastructure.Export;

/// <summary>Génère un fichier Excel professionnel de la liste des factures.</summary>
public static class InvoicesListExcelRenderer
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    private const string Accent = "#1A237E";
    private const string MoneyFormat = "#,##0.00 \"DA\"";

    public static byte[] Render(IEnumerable<InvoiceSummaryDto> invoices, DocumentStrings strings, CompanyBlock company)
    {
        var s = strings;
        var list = invoices.ToList();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Factures");

        // ---- titre
        ws.Cell(1, 1).Value = s.Facture;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 16;
        ws.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml(Accent);
        ws.Cell(1, 3).Value = company.Name;
        ws.Cell(1, 3).Style.Font.Bold = true;
        ws.Cell(1, 3).Style.Font.FontSize = 11;
        ws.Cell(2, 1).Value = DateTime.Now.ToString("dd/MM/yyyy HH:mm", Fr);

        // ---- entête
        const int headerRow = 4;
        var headers = new[]
        {
            s.Number, s.ClientName, s.InvoiceDate, s.DueDateShort, s.Type, s.Status,
            s.TotalHT, s.TotalVat, s.TotalTTC, s.Payee, s.Solde,
        };
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(headerRow, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontSize = 9;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(Accent);
            cell.Style.Alignment.Horizontal = i >= 6 ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Left;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        ws.Row(headerRow).Height = 18;

        var row = headerRow + 1;
        foreach (var inv in list)
        {
            ws.Cell(row, 1).Value = inv.InvoiceNumber;
            ws.Cell(row, 2).Value = inv.ClientName;
            ws.Cell(row, 3).Value = inv.InvoiceDate.ToString("dd/MM/yyyy", Fr);
            ws.Cell(row, 4).Value = inv.DueDate?.ToString("dd/MM/yyyy", Fr) ?? "—";
            ws.Cell(row, 5).Value = TypeLabel(inv.InvoiceType, s);
            ws.Cell(row, 6).Value = s.StatusText(inv.Status);
            ws.Cell(row, 7).Value = (double)inv.TotalHT;
            ws.Cell(row, 8).Value = (double)inv.TotalTVA;
            ws.Cell(row, 9).Value = (double)inv.TotalTTC;
            ws.Cell(row, 10).Value = (double)inv.MontantPaye;
            ws.Cell(row, 11).Value = (double)inv.SoldeRestant;

            for (var c = 1; c <= headers.Length; c++)
            {
                var cell = ws.Cell(row, c);
                cell.Style.Font.FontSize = 9;
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.BottomBorderColor = XLColor.FromHtml("#CBD5E1");
                if (c >= 7)
                {
                    cell.Style.NumberFormat.Format = MoneyFormat;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                }
            }

            row++;
        }

        // ---- totaux
        if (list.Count > 0)
        {
            var totalRow = row + 1;
            ws.Cell(totalRow, 2).Value = s.GrandTotal;
            ws.Cell(totalRow, 2).Style.Font.Bold = true;
            ws.Cell(totalRow, 2).Style.Font.FontSize = 10;

            var moneyCols = new[] { 7, 8, 9, 10, 11 };
            foreach (var c in moneyCols)
            {
                var cell = ws.Cell(totalRow, c);
                cell.FormulaA1 = $"SUM({CellName(totalRow - list.Count, c)}:{CellName(totalRow - 1, c)})";
                cell.Style.NumberFormat.Format = MoneyFormat;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EAF6");
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            }

            row = totalRow + 1;
        }

        // ---- mise en forme
        ws.Column(1).Width = 16;
        ws.Column(2).Width = 28;
        ws.Column(3).Width = 12;
        ws.Column(4).Width = 12;
        ws.Column(5).Width = 12;
        ws.Column(6).Width = 12;
        ws.Column(7).Width = 14;
        ws.Column(8).Width = 12;
        ws.Column(9).Width = 14;
        ws.Column(10).Width = 14;
        ws.Column(11).Width = 14;

        ws.SheetView.FreezeRows(headerRow + 1);
        ws.Range(headerRow, 1, Math.Max(row - 1, headerRow), headers.Length)
            .SetAutoFilter();

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

    private static string CellName(int row, int col) => $"{ColumnLetter(col)}{row}";

    private static string ColumnLetter(int col)
    {
        var letter = string.Empty;
        while (col > 0)
        {
            var mod = (col - 1) % 26;
            letter = (char)('A' + mod) + letter;
            col = (col - 1) / 26;
        }

        return letter;
    }

    private static string TypeLabel(InvoiceType type, DocumentStrings s) => type switch
    {
        InvoiceType.ProForma => s.ProForma,
        InvoiceType.Avoir => s.Avoir,
        _ => s.Facture,
    };
}
