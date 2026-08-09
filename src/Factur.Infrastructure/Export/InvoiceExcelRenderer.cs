using System.Globalization;
using System.IO;
using ClosedXML.Excel;

namespace Factur.Infrastructure.Export;

/// <summary>Génère un fichier Excel professionnel d'une facture / avoir / pro-forma.</summary>
public static class InvoiceExcelRenderer
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    private const string Accent = "#1A237E";
    private const string AccentLight = "#E8EAF6";
    private const string TextColor = "#1F2937";
    private const string MutedColor = "#64748B";
    private const string MoneyFormat = "#,##0.00 \"DA\"";

    public static byte[] Render(ExportDocument doc)
    {
        var s = doc.Strings;

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(doc.Title);

        ws.SheetView.FreezeRows(14);

        // ---- dimensions
        ws.Column(1).Width = 5;
        ws.Column(2).Width = 22;
        ws.Column(3).Width = 30;
        ws.Column(4).Width = 12;
        ws.Column(5).Width = 16;
        ws.Column(6).Width = 10;
        ws.Column(7).Width = 16;
        ws.Column(8).Width = 16;

        // ---- logo
        if (doc.Company.Logo is { Length: > 0 })
        {
            try
            {
                using var logoStream = new MemoryStream(doc.Company.Logo);
                var pic = ws.AddPicture(logoStream);
                pic.MoveTo(ws.Cell("A1"));
                var maxDim = 56;
                var scale = maxDim / System.Math.Max(pic.Width, pic.Height);
                pic.Scale(scale);
            }
            catch
            {
                // logo illisible : ignoré
            }
        }

        // ---- société
        ws.Cell("C1").Value = doc.Company.Name;
        ws.Cell("C1").Style.Font.Bold = true;
        ws.Cell("C1").Style.Font.FontSize = 16;
        ws.Cell("C1").Style.Font.FontColor = XLColor.FromHtml(Accent);

        ws.Cell("C2").Value = CompanyContactLine(doc.Company);
        ws.Cell("C2").Style.Font.FontSize = 8;
        ws.Cell("C2").Style.Font.FontColor = XLColor.FromHtml(MutedColor);

        ws.Cell("C3").Value = CompanyFiscalLine(doc.Company);
        ws.Cell("C3").Style.Font.FontSize = 8;
        ws.Cell("C3").Style.Font.FontColor = XLColor.FromHtml(MutedColor);

        ws.Cell("C4").Value = CompanyBankLine(doc.Company);
        ws.Cell("C4").Style.Font.FontSize = 8;
        ws.Cell("C4").Style.Font.FontColor = XLColor.FromHtml(MutedColor);

        // ---- titre + métadonnées
        ws.Range("G1:H1").Merge();
        ws.Cell("G1").Value = doc.Title;
        ws.Cell("G1").Style.Font.Bold = true;
        ws.Cell("G1").Style.Font.FontSize = 16;
        ws.Cell("G1").Style.Font.FontColor = XLColor.White;
        ws.Cell("G1").Style.Fill.BackgroundColor = XLColor.FromHtml(Accent);
        ws.Cell("G1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Cell("G1").Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        var meta = new[]
        {
            (s.Number, doc.InvoiceNumber),
            (s.IssueDate, doc.IssueDate.ToString("dd/MM/yyyy", Fr)),
            (s.DueDate, doc.DueDate?.ToString("dd/MM/yyyy", Fr) ?? "—"),
            (s.PaymentMethodLabel, doc.PaymentMethod),
        }.ToList();
        if (!string.IsNullOrWhiteSpace(doc.OrderReference))
        {
            meta.Add((s.OrderReference, doc.OrderReference));
        }

        for (var i = 0; i < meta.Count; i++)
        {
            var metaRow = 2 + i;
            ws.Cell(metaRow, 7).Value = meta[i].Item1;
            ws.Cell(metaRow, 7).Style.Font.FontSize = 8;
            ws.Cell(metaRow, 7).Style.Font.FontColor = XLColor.FromHtml(MutedColor);
            ws.Cell(metaRow, 8).Value = meta[i].Item2;
            ws.Cell(metaRow, 8).Style.Font.FontSize = 8;
            ws.Cell(metaRow, 8).Style.Font.Bold = true;
            ws.Cell(metaRow, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        }

        var statusRow = 2 + meta.Count;
        ws.Cell(statusRow, 7).Value = s.StatusLabel;
        ws.Cell(statusRow, 7).Style.Font.FontSize = 8;
        ws.Cell(statusRow, 7).Style.Font.FontColor = XLColor.FromHtml(MutedColor);
        ws.Cell(statusRow, 8).Value = doc.Status;
        ws.Cell(statusRow, 8).Style.Font.Bold = true;
        ws.Cell(statusRow, 8).Style.Font.FontSize = 9;
        ws.Cell(statusRow, 8).Style.Font.FontColor = XLColor.FromHtml(doc.StatusColorHex);
        ws.Cell(statusRow, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        // ---- client
        ws.Cell("A8").Value = s.BillTo;
        ws.Cell("A8").Style.Font.Bold = true;
        ws.Cell("A8").Style.Font.FontSize = 8;
        ws.Cell("A8").Style.Font.FontColor = XLColor.FromHtml(Accent);

        ws.Cell("A9").Value = doc.Client.Name;
        ws.Cell("A9").Style.Font.Bold = true;
        ws.Cell("A9").Style.Font.FontSize = 12;

        var clientLines = new List<string>();
        if (!string.IsNullOrWhiteSpace(doc.Client.Address))
        {
            clientLines.Add(doc.Client.Address);
        }

        if (!string.IsNullOrWhiteSpace(PartyFiscalLine(doc.Client)))
        {
            clientLines.Add(PartyFiscalLine(doc.Client));
        }

        if (!string.IsNullOrWhiteSpace(PartyContactLine(doc.Client)))
        {
            clientLines.Add(PartyContactLine(doc.Client));
        }

        for (var i = 0; i < clientLines.Count; i++)
        {
            ws.Cell(10 + i, 1).Value = clientLines[i];
            ws.Cell(10 + i, 1).Style.Font.FontSize = 8;
            ws.Cell(10 + i, 1).Style.Font.FontColor = XLColor.FromHtml(MutedColor);
        }

        if (doc.Totals.MontantPaye > 0m)
        {
            ws.Cell(10, 7).Value = $"{s.AmountPaid} : {Money(doc.Totals.MontantPaye)}";
            ws.Cell(10, 7).Style.Font.FontSize = 8;
            ws.Cell(11, 7).Value = $"{s.BalanceDue} : {Money(doc.Totals.SoldeRestant)}";
            ws.Cell(11, 7).Style.Font.FontSize = 8;
            ws.Cell(11, 7).Style.Font.Bold = true;
        }

        // ---- table des lignes
        const int headerRow = 14;
        var headers = new[] { s.Index, s.Reference, s.Designation, s.Quantity, s.UnitPrice, s.Vat, s.AmountHT, s.AmountTTC };
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(headerRow, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontSize = 9;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(Accent);
            cell.Style.Alignment.Horizontal = i >= 3 ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Left;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.BottomBorderColor = XLColor.FromHtml(Accent);
        }

        ws.Row(headerRow).Height = 18;

        var row = headerRow + 1;
        foreach (var line in doc.Lines)
        {
            ws.Cell(row, 1).Value = line.Index;
            ws.Cell(row, 2).Value = line.Reference;
            ws.Cell(row, 3).Value = line.Designation;
            ws.Cell(row, 4).Value = (double)line.Quantity;
            ws.Cell(row, 5).Value = (double)line.UnitPriceHT;
            ws.Cell(row, 6).Value = line.VatLabel;
            ws.Cell(row, 7).Value = (double)line.TotalHT;
            ws.Cell(row, 8).Value = (double)line.TotalTTC;

            for (var c = 1; c <= 8; c++)
            {
                var cell = ws.Cell(row, c);
                cell.Style.Font.FontSize = 9;
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.BottomBorderColor = XLColor.FromHtml("#CBD5E1");
                if (c >= 4)
                {
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                }

                if (c is 4)
                {
                    cell.Style.NumberFormat.Format = "0.##";
                }

                if (c is 5 or 7 or 8)
                {
                    cell.Style.NumberFormat.Format = MoneyFormat;
                }
            }

            row++;
        }

        // ---- récap TVA + totaux
        var tvaStart = row + 1;
        ws.Cell(tvaStart, 1).Value = s.VatSummary;
        ws.Cell(tvaStart, 1).Style.Font.Bold = true;
        ws.Cell(tvaStart, 1).Style.Font.FontSize = 8;
        ws.Cell(tvaStart, 1).Style.Font.FontColor = XLColor.FromHtml(Accent);

        var tvaRows = doc.VatBreakdowns.Count > 0
            ? doc.VatBreakdowns.Select(b => (b.Label, b.BaseHT, b.VatAmount, b.Ttc)).ToList()
            : new List<(string, decimal, decimal, decimal)> { ("—", doc.Totals.TotalHT, doc.Totals.TotalTVA, doc.Totals.TotalTTC) };

        var tvh = tvaStart + 1;
        var tvaHeaders = new[] { s.Rate, s.Base, s.VatAmount, s.Ttc };
        for (var i = 0; i < tvaHeaders.Length; i++)
        {
            var cell = ws.Cell(tvh, i + 1);
            cell.Value = tvaHeaders[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontSize = 8;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(Accent);
            cell.Style.Alignment.Horizontal = i > 0 ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Left;
        }

        var tr = tvh + 1;
        foreach (var (label, baseHt, vat, ttc) in tvaRows)
        {
            ws.Cell(tr, 1).Value = label;
            ws.Cell(tr, 2).Value = (double)baseHt;
            ws.Cell(tr, 3).Value = (double)vat;
            ws.Cell(tr, 4).Value = (double)ttc;
            for (var c = 1; c <= 4; c++)
            {
                var cell = ws.Cell(tr, c);
                cell.Style.Font.FontSize = 8;
                if (c > 1)
                {
                    cell.Style.NumberFormat.Format = MoneyFormat;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                }
            }

            tr++;
        }

        var totalStart = tr + 1;
        var totalList = new List<(string Label, decimal Value, bool Negative, bool Bold, bool AccentFill)>
        {
            (s.Subtotal, doc.Totals.TotalHT, false, false, false),
        };
        if (doc.Totals.RemiseAmount > 0m)
        {
            var label = string.IsNullOrWhiteSpace(doc.Totals.RemiseLabel)
                ? s.DiscountDetail
                : $"{s.DiscountDetail} ({doc.Totals.RemiseLabel})";
            totalList.Add((label, doc.Totals.RemiseAmount, true, false, false));
        }

        totalList.Add((s.TotalVat, doc.Totals.TotalTVA, false, false, false));
        if (doc.Totals.FraisPort is > 0m)
        {
            totalList.Add((doc.Totals.FraisPortLabel ?? s.Shipping, doc.Totals.FraisPort.Value, false, false, false));
        }

        if (doc.Totals.AutresFrais is > 0m)
        {
            totalList.Add((doc.Totals.AutresFraisLabel ?? s.OtherFees, doc.Totals.AutresFrais.Value, false, false, false));
        }

        totalList.Add((s.TotalTTC, doc.Totals.TotalTTC, false, true, true));
        if (doc.Totals.MontantPaye > 0m)
        {
            totalList.Add((s.AmountPaid, doc.Totals.MontantPaye, false, false, false));
            totalList.Add((s.BalanceDue, doc.Totals.SoldeRestant, false, true, false));
        }

        var tt = totalStart;
        foreach (var (label, value, negative, bold, accentFill) in totalList)
        {
            ws.Cell(tt, 5).Value = label;
            ws.Cell(tt, 5).Style.Font.FontSize = 8;
            ws.Cell(tt, 5).Style.Font.Bold = bold;
            ws.Cell(tt, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(tt, 8).Value = (double)(negative ? -value : value);
            ws.Cell(tt, 8).Style.NumberFormat.Format = MoneyFormat;
            ws.Cell(tt, 8).Style.Font.FontSize = 8;
            ws.Cell(tt, 8).Style.Font.Bold = bold;
            ws.Cell(tt, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            if (accentFill)
            {
                ws.Cell(tt, 5).Style.Fill.BackgroundColor = XLColor.FromHtml(Accent);
                ws.Cell(tt, 5).Style.Font.FontColor = XLColor.White;
                ws.Cell(tt, 8).Style.Fill.BackgroundColor = XLColor.FromHtml(Accent);
                ws.Cell(tt, 8).Style.Font.FontColor = XLColor.White;
            }

            tt++;
        }

        // ---- montant en lettres
        if (!string.IsNullOrWhiteSpace(doc.AmountInWords))
        {
            tt += 1;
            ws.Cell(tt, 1).Value = $"{s.AmountInWordsLabel} {doc.AmountInWords}";
            ws.Cell(tt, 1).Style.Font.FontSize = 9;
            ws.Cell(tt, 1).Style.Font.Italic = true;
            ws.Cell(tt, 1).Style.Alignment.WrapText = true;
        }

        // ---- notes
        var hasNotes = doc.PaymentConditions is { Length: > 0 } || doc.Penalties is { Length: > 0 }
                       || doc.MentionsSpecifiques is { Length: > 0 } || doc.Notes is { Length: > 0 };
        if (hasNotes)
        {
            tt += 1;
            ws.Cell(tt, 1).Value = s.ConditionsAndMentions;
            ws.Cell(tt, 1).Style.Font.Bold = true;
            ws.Cell(tt, 1).Style.Font.FontSize = 8;
            ws.Cell(tt, 1).Style.Font.FontColor = XLColor.FromHtml(Accent);
            tt++;
            if (doc.PaymentConditions is { Length: > 0 })
            {
                ws.Cell(tt, 1).Value = $"{s.PaymentConditions} : {doc.PaymentConditions}";
                ws.Cell(tt, 1).Style.Font.FontSize = 8;
                ws.Cell(tt, 1).Style.Alignment.WrapText = true;
                tt++;
            }

            if (doc.Penalties is { Length: > 0 })
            {
                ws.Cell(tt, 1).Value = $"{s.LatePenalties} : {doc.Penalties}";
                ws.Cell(tt, 1).Style.Font.FontSize = 8;
                ws.Cell(tt, 1).Style.Alignment.WrapText = true;
                tt++;
            }

            if (doc.MentionsSpecifiques is { Length: > 0 })
            {
                ws.Cell(tt, 1).Value = doc.MentionsSpecifiques;
                ws.Cell(tt, 1).Style.Font.FontSize = 8;
                ws.Cell(tt, 1).Style.Alignment.WrapText = true;
                tt++;
            }

            if (doc.Notes is { Length: > 0 })
            {
                ws.Cell(tt, 1).Value = $"{s.Notes} : {doc.Notes}";
                ws.Cell(tt, 1).Style.Font.FontSize = 8;
                ws.Cell(tt, 1).Style.Alignment.WrapText = true;
            }
        }

        // ---- impression
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

    private static string Money(decimal v) => v.ToString("N2", Fr);

    private static string CompanyFiscalLine(CompanyBlock c)
    {
        var parts = new[]
        {
            string.IsNullOrWhiteSpace(c.NIF) ? null : $"NIF : {c.NIF}",
            string.IsNullOrWhiteSpace(c.NIS) ? null : $"NIS : {c.NIS}",
            string.IsNullOrWhiteSpace(c.RC) ? null : $"RC : {c.RC}",
            string.IsNullOrWhiteSpace(c.ART) ? null : $"ART : {c.ART}",
        };
        return string.Join("   ", parts.Where(x => x is not null));
    }

    private static string CompanyBankLine(CompanyBlock c)
    {
        var parts = new[]
        {
            string.IsNullOrWhiteSpace(c.BankName) ? null : c.BankName,
            string.IsNullOrWhiteSpace(c.RIB) ? null : $"RIB : {c.RIB}",
            string.IsNullOrWhiteSpace(c.CCP) ? null : $"CCP : {c.CCP}",
        };
        return string.Join("   ", parts.Where(x => x is not null));
    }

    private static string CompanyContactLine(CompanyBlock c)
    {
        var parts = new[] { c.Address, PhoneText(c.Phone), c.Email }.Where(x => !string.IsNullOrWhiteSpace(x));
        return string.Join("   ", parts);
    }

    private static string PartyFiscalLine(PartyBlock p)
    {
        var parts = new[]
        {
            string.IsNullOrWhiteSpace(p.NIF) ? null : $"NIF : {p.NIF}",
            string.IsNullOrWhiteSpace(p.RC) ? null : $"RC : {p.RC}",
            string.IsNullOrWhiteSpace(p.ART) ? null : $"ART : {p.ART}",
        };
        return string.Join("   ", parts.Where(x => x is not null));
    }

    private static string PartyContactLine(PartyBlock p)
    {
        var parts = new[] { PhoneText(p.Phone), p.Email }.Where(x => !string.IsNullOrWhiteSpace(x));
        return string.Join("   ", parts);
    }

    private static string PhoneText(string? phone) => string.IsNullOrWhiteSpace(phone) ? string.Empty : $"Tél : {phone}";
}
