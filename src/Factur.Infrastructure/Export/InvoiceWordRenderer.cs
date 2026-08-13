using System.Globalization;
using System.IO;
using Factur.Domain;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Drawing.Pictures;
using DocumentFormat.OpenXml.Drawing.Wordprocessing;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using Pic = DocumentFormat.OpenXml.Drawing.Pictures;
using WordCell = DocumentFormat.OpenXml.Wordprocessing.TableCell;
using WordParagraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using WordRun = DocumentFormat.OpenXml.Wordprocessing.Run;
using Text = DocumentFormat.OpenXml.Wordprocessing.Text;
using WordColor = DocumentFormat.OpenXml.Wordprocessing.Color;

namespace Factur.Infrastructure.Export;

/// <summary>Génère un document Word professionnel (OpenXML) d'une facture / avoir / pro-forma.</summary>
public static class InvoiceWordRenderer
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    private const string Accent = "1A237E";
    private const string AccentLight = "E8EAF6";
    private const string TextColor = "1F2937";
    private const string MutedColor = "64748B";

    public static byte[] Render(ExportDocument doc, TypographyOptions? typography = null)
    {
        var s = doc.Strings;
        var typo = typography ?? new TypographyOptions();
        var font = typo.FontFamily;
        var headerSz = (int)Math.Round(typo.HeaderFontSize * 2);
        var tableSz = (int)Math.Round(typo.TableFontSize * 2);
        var baseSz = (int)Math.Round(typo.BaseFontSize * 2);
        var footerSz = (int)Math.Round(typo.FooterFontSize * 2);

        using var ms = new MemoryStream();
        using (var word = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var main = word.AddMainDocumentPart();
            main.Document = new Document();
            var body = main.Document.AppendChild(new Body());

            // ---- sections / page
            var sectPr = new SectionProperties();

            // ---- en-tête (répété sur chaque page)
            var headerPart = main.AddNewPart<HeaderPart>();
            var header = new Header(new WordParagraph(new ParagraphProperties(new ParagraphStyleId { Val = "Header" })));
            headerPart.Header = header;
            header.Append(BuildHeaderParagraph(doc, headerPart, headerSz, font));
            sectPr.AppendChild(new HeaderReference { Type = HeaderFooterValues.Default, Id = "rIdHeader" });
            main.AddPart(headerPart);

            // ---- pied de page
            var footerPart = main.AddNewPart<FooterPart>();
            var footer = new Footer();
            footer.Append(BuildFooterParagraph(doc, footerSz, font));
            footerPart.Footer = footer;
            sectPr.AppendChild(new FooterReference { Type = HeaderFooterValues.Default, Id = "rIdFooter" });
            main.AddPart(footerPart);

            // ---- taille de page
            sectPr.AppendChild(new PageSize { Width = 11906, Height = 16838 }); // A4
            sectPr.AppendChild(new PageMargin
            {
                Top = 1200, Right = 1134, Bottom = 900, Left = 1134,
                Header = 360, Footer = 360, Gutter = 0,
            });
            body.AppendChild(sectPr);

            // ---- corps
            AppendClientBlock(body, doc, baseSz, font);
            AppendLinesTable(body, doc, headerSz, tableSz, font);
            AppendVatAndTotals(body, doc, headerSz, tableSz, baseSz, font);
            AppendAmountInWords(body, doc, baseSz, font);
            AppendNotes(body, doc, baseSz, font);
            AppendBottomSpacer(body);
        }

        return ms.ToArray();
    }

    // ---------------------------------------------------------------- header / footer

    private static WordParagraph BuildHeaderParagraph(ExportDocument doc, HeaderPart headerPart, int headerSz, string? font)
    {
        var p = new WordParagraph();
        p.AppendChild(new ParagraphProperties(new SpacingBetweenLines { After = "0" }));

        if (doc.Company.Logo is { Length: > 0 })
        {
            var imagePart = headerPart.AddNewPart<ImagePart>(ImageContentType(doc.Company.Logo), "rIdLogo");
            using var stream = new MemoryStream(doc.Company.Logo);
            imagePart.FeedData(stream);

            p.AppendChild(new WordRun(ImageRun("rIdLogo", 500000, 500000)));
            p.AppendChild(TextRun("   ", size: 16, font: font));
        }

        p.AppendChild(TextRun(doc.Company.Name, bold: true, size: headerSz, color: Accent, font: font));
        p.AppendChild(TextRun("   ", size: 16, font: font));
        p.AppendChild(TextRun($"N° {doc.InvoiceNumber}", bold: true, size: headerSz, color: Accent, font: font));

        var contact = string.Join("   ", new[]
        {
            CompanyFiscalLine(doc.Company),
            CompanyContactLine(doc.Company),
        }.Where(x => !string.IsNullOrWhiteSpace(x)));

        if (!string.IsNullOrWhiteSpace(contact))
        {
            var contactRun = new WordRun(
                new RunProperties(
                    new RunFonts { Ascii = font ?? "Calibri", HighAnsi = font ?? "Calibri" },
                    new WordColor { Val = MutedColor },
                    new FontSize { Val = "16" }),
                new Break(),
                new Text(contact) { Space = SpaceProcessingModeValues.Preserve });
            p.Append(contactRun);
        }

        return p;
    }

    private static WordParagraph BuildFooterParagraph(ExportDocument doc, int footerSz, string? font)
    {
        var s = doc.Strings;
        var p = new WordParagraph(new ParagraphProperties(new Justification { Val = JustificationValues.Right }));

        p.Append(TextRun($"{s.Page} ", size: footerSz, color: MutedColor, font: font));
        p.Append(FieldRun(" PAGE ", footerSz, font));
        p.Append(TextRun($" {s.Of} ", size: footerSz, color: MutedColor, font: font));
        p.Append(FieldRun(" NUMPAGES ", footerSz, font));

        return p;
    }

    private static WordRun FieldRun(string instruction, int footerSz, string? font)
    {
        var props = new RunProperties(
            new RunFonts { Ascii = font ?? "Calibri", HighAnsi = font ?? "Calibri" },
            new WordColor { Val = MutedColor },
            new FontSize { Val = footerSz.ToString() });
        var WordRun = new WordRun();
        WordRun.Append(props);
        WordRun.Append(new FieldChar { FieldCharType = FieldCharValues.Begin });
        WordRun.Append(new FieldCode(instruction) { Space = SpaceProcessingModeValues.Preserve });
        WordRun.Append(new FieldChar { FieldCharType = FieldCharValues.Separate });
        WordRun.Append(new Text("1"));
        WordRun.Append(new FieldChar { FieldCharType = FieldCharValues.End });
        return WordRun;
    }

    private static string ImageContentType(byte[] data)
    {
        var signature = data.Length >= 8 ? BitConverter.ToString(data[..8]) : string.Empty;
        if (signature.StartsWith("89-50-4E-47")) return "image/png";
        if (signature.StartsWith("FF-D8-FF")) return "image/jpeg";
        if (signature.StartsWith("47-49-46-38")) return "image/gif";
        if (signature.StartsWith("42-4D")) return "image/bmp";
        return "image/png";
    }

    private static OpenXmlElement ImageRun(string relationshipId, long widthEmu, long heightEmu)
    {
        var element = new Drawing(
            new Inline(
                new Extent { Cx = widthEmu, Cy = heightEmu },
                new EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
                new DocProperties { Id = 1, Name = "logo" },
                new A.Graphic(new A.GraphicData(
                    new Pic.Picture(
                        new Pic.NonVisualPictureProperties(
                            new Pic.NonVisualDrawingProperties { Id = 1, Name = "logo" },
                            new Pic.NonVisualPictureDrawingProperties()),
                        new Pic.BlipFill(
                            new A.Blip { Embed = relationshipId },
                            new A.Stretch(new A.FillRectangle())),
                        new Pic.ShapeProperties(
                            new A.Transform2D(new A.Offset { X = 0, Y = 0 }, new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            { DistanceFromTop = 0, DistanceFromBottom = 0, DistanceFromLeft = 0, DistanceFromRight = 0 });
        return element;
    }

    // ---------------------------------------------------------------- corps

    private static void AppendClientBlock(Body body, ExportDocument doc, int baseSz, string? font)
    {
        var s = doc.Strings;
        body.AppendChild(Spacer(80));
        body.AppendChild(LabelParagraph(s.BillTo, Accent, font));
        body.AppendChild(ParagraphText(doc.Client.Name, bold: true, size: baseSz, font: font));
        if (!string.IsNullOrWhiteSpace(doc.Client.Address))
        {
            body.AppendChild(ParagraphText(doc.Client.Address, size: baseSz, font: font));
        }

        var info = PartyFiscalLine(doc.Client);
        if (!string.IsNullOrWhiteSpace(info))
        {
            body.AppendChild(ParagraphText(info, size: baseSz, color: MutedColor, font: font));
        }

        body.AppendChild(Spacer(120));

        var meta = new List<(string Label, string Value)>
        {
            (s.Number, doc.InvoiceNumber),
            (s.IssueDate, doc.IssueDate.ToString("dd/MM/yyyy", Fr)),
            (s.DueDate, doc.DueDate?.ToString("dd/MM/yyyy", Fr) ?? "—"),
            (s.PaymentMethodLabel, doc.PaymentMethod),
        };
        if (!string.IsNullOrWhiteSpace(doc.OrderReference))
        {
            meta.Add((s.OrderReference, doc.OrderReference));
        }

        var metaTable = new Table();
        metaTable.AppendChild(new TableProperties(new TableLayout { Type = TableLayoutValues.Fixed })
        {
            TableBorders = new TableBorders(new TopBorder { Val = new EnumValue<BorderValues>(BorderValues.None) },
                new BottomBorder { Val = new EnumValue<BorderValues>(BorderValues.None) },
                new LeftBorder { Val = new EnumValue<BorderValues>(BorderValues.None) },
                new RightBorder { Val = new EnumValue<BorderValues>(BorderValues.None) },
                new InsideHorizontalBorder { Val = new EnumValue<BorderValues>(BorderValues.None) },
                new InsideVerticalBorder { Val = new EnumValue<BorderValues>(BorderValues.None) }),
            TableWidth = new TableWidth { Width = "5200", Type = TableWidthUnitValues.Dxa },
        });
        foreach (var (label, value) in meta)
        {
            var tr = new TableRow();
            tr.Append(SimpleCell(label, width: 1600, bold: false, color: MutedColor, size: baseSz, font: font));
            tr.Append(SimpleCell(value, width: 3600, bold: true, size: baseSz, font: font));
            metaTable.Append(tr);
        }

        body.AppendChild(metaTable);
        body.AppendChild(Spacer(160));
    }

    private static void AppendLinesTable(Body body, ExportDocument doc, int headerSz, int tableSz, string? font)
    {
        var s = doc.Strings;
        var table = new Table();
        table.AppendChild(new TableProperties(new TableLayout { Type = TableLayoutValues.Fixed })
        {
            TableBorders = new TableBorders(
                new TopBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 8, Color = Accent },
                new BottomBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4, Color = Accent },
                new LeftBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 2, Color = Accent },
                new RightBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 2, Color = Accent },
                new InsideHorizontalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 2, Color = "CBD5E1" },
                new InsideVerticalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 2, Color = "CBD5E1" }),
            TableWidth = new TableWidth { Width = "9600", Type = TableWidthUnitValues.Dxa },
        });

        // en-tête répété sur chaque page
        var headerRow = new TableRow(new TableRowProperties(new TableHeader()));
        var widths = new[] { 400, 1200, 2700, 800, 1300, 800, 1200, 1200 };
        var headers = new[] { s.Index, s.Reference, s.Designation, s.Quantity, s.UnitPrice, s.Vat, s.AmountHT, s.AmountTTC };
        var align = new[] { Align.Center, Align.Left, Align.Left, Align.Right, Align.Right, Align.Right, Align.Right, Align.Right };
        for (var i = 0; i < headers.Length; i++)
        {
            headerRow.Append(SimpleCell(headers[i], widths[i], bold: true, color: "#FFFFFF", size: headerSz, fill: Accent, align: align[i], font: font));
        }

        table.Append(headerRow);

        foreach (var line in doc.Lines)
        {
            var tr = new TableRow();
            tr.Append(SimpleCell(line.Index.ToString(), widths[0], bold: true, size: tableSz, align: Align.Center, font: font));
            tr.Append(SimpleCell(line.Reference, widths[1], size: tableSz, font: font));
            tr.Append(SimpleCell(line.Designation, widths[2], size: tableSz, font: font));
            tr.Append(SimpleCell(Qty(line.Quantity), widths[3], size: tableSz, align: Align.Right, font: font));
            tr.Append(SimpleCell(Money(line.UnitPriceHT), widths[4], size: tableSz, align: Align.Right, font: font));
            tr.Append(SimpleCell(line.VatLabel, widths[5], size: tableSz, align: Align.Right, font: font));
            tr.Append(SimpleCell(Money(line.TotalHT), widths[6], size: tableSz, align: Align.Right, font: font));
            tr.Append(SimpleCell(Money(line.TotalTTC), widths[7], size: tableSz, align: Align.Right, font: font));
            table.Append(tr);
        }

        body.AppendChild(table);
        body.AppendChild(Spacer(120));
    }

    private static void AppendVatAndTotals(Body body, ExportDocument doc, int headerSz, int tableSz, int baseSz, string? font)
    {
        var s = doc.Strings;

        if (doc.VatBreakdowns.Count > 0)
        {
            body.AppendChild(LabelParagraph(s.VatSummary, Accent, font));
            body.AppendChild(Spacer(40));
            var vatTable = new Table();
            vatTable.AppendChild(new TableProperties(new TableLayout { Type = TableLayoutValues.Fixed })
            {
                TableBorders = new TableBorders(
                    new TopBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4, Color = Accent },
                    new BottomBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4, Color = Accent },
                    new LeftBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 2, Color = Accent },
                    new RightBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 2, Color = Accent },
                    new InsideHorizontalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 2, Color = "CBD5E1" },
                    new InsideVerticalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 2, Color = "CBD5E1" }),
                TableWidth = new TableWidth { Width = "4600", Type = TableWidthUnitValues.Dxa },
            });

            var hdr = new TableRow(new TableRowProperties(new TableHeader()));
            hdr.Append(SimpleCell(s.Rate, 900, bold: true, color: "#FFFFFF", size: headerSz, fill: Accent, font: font));
            hdr.Append(SimpleCell(s.Base, 1250, bold: true, color: "#FFFFFF", size: headerSz, fill: Accent, align: Align.Right, font: font));
            hdr.Append(SimpleCell(s.VatAmount, 1250, bold: true, color: "#FFFFFF", size: headerSz, fill: Accent, align: Align.Right, font: font));
            hdr.Append(SimpleCell(s.Ttc, 1200, bold: true, color: "#FFFFFF", size: headerSz, fill: Accent, align: Align.Right, font: font));
            vatTable.Append(hdr);

            foreach (var b in doc.VatBreakdowns)
            {
                var tr = new TableRow();
                tr.Append(SimpleCell(b.Label, 900, size: tableSz, font: font));
                tr.Append(SimpleCell(Money(b.BaseHT), 1250, size: tableSz, align: Align.Right, font: font));
                tr.Append(SimpleCell(Money(b.VatAmount), 1250, size: tableSz, align: Align.Right, font: font));
                tr.Append(SimpleCell(Money(b.Ttc), 1200, size: tableSz, align: Align.Right, font: font));
                vatTable.Append(tr);
            }

            body.AppendChild(vatTable);
            body.AppendChild(Spacer(120));
        }

        // totaux
        var totalTable = new Table();
        totalTable.AppendChild(new TableProperties(new TableLayout { Type = TableLayoutValues.Fixed })
        {
            TableBorders = new TableBorders(new TopBorder { Val = new EnumValue<BorderValues>(BorderValues.None) },
                new BottomBorder { Val = new EnumValue<BorderValues>(BorderValues.None) },
                new LeftBorder { Val = new EnumValue<BorderValues>(BorderValues.None) },
                new RightBorder { Val = new EnumValue<BorderValues>(BorderValues.None) },
                new InsideHorizontalBorder { Val = new EnumValue<BorderValues>(BorderValues.None) },
                new InsideVerticalBorder { Val = new EnumValue<BorderValues>(BorderValues.None) }),
            TableWidth = new TableWidth { Width = "4600", Type = TableWidthUnitValues.Dxa },
            TableJustification = new TableJustification { Val = TableRowAlignmentValues.Right },
        });

        var rows = new List<(string Label, string Value, bool Bold, string? Fill)>
        {
            (s.Subtotal, Money(doc.Totals.TotalHT), false, null),
        };
        if (doc.Totals.RemiseAmount > 0m)
        {
            var label = string.IsNullOrWhiteSpace(doc.Totals.RemiseLabel)
                ? s.DiscountDetail
                : $"{s.DiscountDetail} ({doc.Totals.RemiseLabel})";
            rows.Add((label, "- " + Money(doc.Totals.RemiseAmount), false, null));
        }

        rows.Add((s.TotalVat, Money(doc.Totals.TotalTVA), false, null));
        if (doc.Totals.FraisPort is > 0m)
        {
            rows.Add((doc.Totals.FraisPortLabel ?? s.Shipping, Money(doc.Totals.FraisPort.Value), false, null));
        }

        if (doc.Totals.AutresFrais is > 0m)
        {
            rows.Add((doc.Totals.AutresFraisLabel ?? s.OtherFees, Money(doc.Totals.AutresFrais.Value), false, null));
        }

        rows.Add((s.TotalTTC, Money(doc.Totals.TotalTTC), true, Accent));
        if (doc.Totals.MontantPaye > 0m)
        {
            rows.Add((s.AmountPaid, "- " + Money(doc.Totals.MontantPaye), false, null));
            rows.Add((s.BalanceDue, Money(doc.Totals.SoldeRestant), true, null));
        }

        foreach (var (label, value, bold, fill) in rows)
        {
            var tr = new TableRow();
            tr.Append(SimpleCell(label, 3100, bold: bold, size: tableSz, fill: fill, color: bold && fill != null ? "#FFFFFF" : TextColor, font: font));
            tr.Append(SimpleCell(value, 1500, bold: bold, size: tableSz, fill: fill, color: bold && fill != null ? "#FFFFFF" : TextColor, align: Align.Right, font: font));
            totalTable.Append(tr);
        }

        body.AppendChild(totalTable);
    }

    private static void AppendAmountInWords(Body body, ExportDocument doc, int baseSz, string? font)
    {
        if (string.IsNullOrWhiteSpace(doc.AmountInWords))
        {
            return;
        }

        body.AppendChild(Spacer(200));
        var p = new WordParagraph(new ParagraphProperties(new SpacingBetweenLines { After = "0" }));
        p.Append(TextRun($"{doc.Strings.AmountInWordsLabel} ", bold: true, size: baseSz, font: font));
        p.Append(TextRun(doc.AmountInWords, italic: true, size: baseSz, font: font));
        body.AppendChild(p);
    }

    private static void AppendNotes(Body body, ExportDocument doc, int baseSz, string? font)
    {
        var s = doc.Strings;
        var hasNotes = doc.PaymentConditions is { Length: > 0 } || doc.Penalties is { Length: > 0 }
                       || doc.MentionsSpecifiques is { Length: > 0 } || doc.Notes is { Length: > 0 };
        if (!hasNotes)
        {
            return;
        }

        body.AppendChild(Spacer(200));
        body.AppendChild(LabelParagraph(s.ConditionsAndMentions, Accent, font));
        if (doc.PaymentConditions is { Length: > 0 })
        {
            body.AppendChild(ParagraphText($"{s.PaymentConditions} : {doc.PaymentConditions}", size: baseSz, font: font));
        }

        if (doc.Penalties is { Length: > 0 })
        {
            body.AppendChild(ParagraphText($"{s.LatePenalties} : {doc.Penalties}", size: baseSz, font: font));
        }

        if (doc.MentionsSpecifiques is { Length: > 0 })
        {
            body.AppendChild(ParagraphText(doc.MentionsSpecifiques, size: baseSz, font: font));
        }

        if (doc.Notes is { Length: > 0 })
        {
            body.AppendChild(ParagraphText($"{s.Notes} : {doc.Notes}", size: baseSz, font: font));
        }
    }

    // ---------------------------------------------------------------- helpers

    private static string Qty(decimal q) => q.ToString("0.##", Fr);
    private static string Money(decimal v) => $"{v.ToString("N2", Fr)} DA";

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

    private static string PhoneText(string? phone) => string.IsNullOrWhiteSpace(phone) ? string.Empty : $"Tél : {phone}";

    private static WordParagraph Spacer(int twips) =>
        new(new ParagraphProperties(new SpacingBetweenLines { After = twips.ToString() }));

    private static void AppendBottomSpacer(Body body) =>
        body.AppendChild(new WordParagraph(new ParagraphProperties(new SpacingBetweenLines { After = "200" })));

    private static WordParagraph LabelParagraph(string text, string color, string? font = null) =>
        ParagraphText(text, bold: true, size: null, color: color, italic: false, font: font);

    private static WordParagraph ParagraphText(string text, bool bold = false, int? size = null, string? color = null, bool italic = false, string? font = null)
    {
        var p = new WordParagraph(new ParagraphProperties(new SpacingBetweenLines { After = "40" }));
        p.Append(TextRun(text, bold: bold, size: size, color: color, italic: italic, font: font));
        return p;
    }

    private static WordRun TextRun(string text, bool bold = false, bool italic = false, int? size = null, string? color = null, string? font = null)
    {
        var props = new RunProperties(new RunFonts { Ascii = font ?? "Calibri", HighAnsi = font ?? "Calibri" });
        if (color is not null)
        {
            props.Append(new WordColor { Val = color });
        }

        if (bold)
        {
            props.Append(new Bold());
        }

        if (italic)
        {
            props.Append(new Italic());
        }

        if (size.HasValue)
        {
            props.Append(new FontSize { Val = (size.Value * 2).ToString() });
        }

        return new WordRun(props, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    private enum Align
    {
        Left,
        Center,
        Right,
    }

    private static WordCell SimpleCell(string text, int width, bool bold = false, string? color = null, int size = 20, string? fill = null, Align align = Align.Left, string? font = null)
    {
        var cellProps = new TableCellProperties(
            new TableCellWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa })
        {
            TableCellVerticalAlignment = new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
        };
        if (fill is not null)
        {
            cellProps.AppendChild(new Shading { Fill = fill });
        }

        var WordParagraph = new WordParagraph(new ParagraphProperties
        {
            SpacingBetweenLines = new SpacingBetweenLines { Before = "30", After = "30" },
            Justification = new Justification { Val = align switch
            {
                Align.Center => JustificationValues.Center,
                Align.Right => JustificationValues.Right,
                _ => JustificationValues.Left,
            } },
        });
        WordParagraph.Append(TextRun(text, bold: bold, size: size, color: color, font: font));

        return new WordCell(cellProps, WordParagraph);
    }
}
