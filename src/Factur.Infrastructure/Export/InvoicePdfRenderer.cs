using System.Globalization;
using Factur.Domain;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Factur.Infrastructure.Export;

/// <summary>Génère un PDF professionnel d'un document (facture / avoir / pro-forma).</summary>
public static class InvoicePdfRenderer
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    private static readonly HashSet<string> SafeFontFamilies = new(StringComparer.OrdinalIgnoreCase)
    {
        "Arial", "Times New Roman", "Calibri", "Georgia", "Consolas", "Courier New", "Inter",
    };

    private static readonly Dictionary<string, string> FontFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Arial"] = "arial.ttf",
        ["Times New Roman"] = "times.ttf",
        ["Calibri"] = "calibri.ttf",
        ["Georgia"] = "georgia.ttf",
        ["Consolas"] = "consola.ttf",
        ["Courier New"] = "cour.ttf",
        ["Inter"] = "Inter-Regular.ttf",
    };

    private static readonly HashSet<string> RegisteredFonts = new(StringComparer.OrdinalIgnoreCase);

    public static byte[] Render(ExportDocument doc, TypographyOptions? typography = null)
    {
        var s = doc.Strings;
        var accent = "#1A237E";
        var typo = typography ?? new TypographyOptions();
        var pdfFont = ResolvePdfFont(typo.FontFamily);

        var baseSize = (float)typo.BaseFontSize;
        var tableSize = (float)typo.TableFontSize;
        var headerSize = (float)typo.HeaderFontSize;
        var footerSize = (float)typo.FooterFontSize;

        void HeaderCell(TableDescriptor table, string text, string accent, bool alignRight = false) =>
            HeaderCellBody(table.Cell(), text, accent, alignRight, tableSize);

        void HeaderCellTcd(TableCellDescriptor table, string text, string accent, bool alignRight = false) =>
            HeaderCellBody(table.Cell(), text, accent, alignRight, tableSize);

        void HeaderCellBody(IContainer raw, string text, string accent, bool alignRight, float size)
        {
            var cell = raw.Element(x => x.Border(0.5f).BorderColor(accent).Background(accent).Padding(4));
            if (alignRight)
            {
                cell = cell.AlignRight();
            }

            cell.Text(text).FontColor("#FFFFFF").SemiBold().FontSize(size);
        }

        void LineCell(TableDescriptor table, string? text, bool bold = false, bool alignRight = false)
        {
            var container = table.Cell().Element(Cell);
            if (alignRight)
            {
                container = container.AlignRight();
            }

            container.Text(t =>
            {
                var span = t.Span(text ?? string.Empty).FontSize(tableSize).FontColor("#334155");
                if (bold)
                {
                    span.SemiBold();
                }
            });
        }

        void Meta(TableDescriptor table, string label, string value, string accent, bool boldValue = false)
        {
            table.Cell().Element(x => x.PaddingVertical(1.5f)).Text(label).FontSize(baseSize).FontColor("#64748B");
            table.Cell().Element(x => x.PaddingVertical(1.5f)).AlignRight().Text(value).FontSize(baseSize).SemiBold();
        }

        var document = Document.Create(d =>
        {
            d.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(26);
                page.MarginTop(40);
                page.MarginBottom(40);
                page.DefaultTextStyle(x =>
                {
                    var style = x.FontSize(baseSize).FontColor("#1F2937");
                    if (pdfFont is not null)
                    {
                        style = style.FontFamily(pdfFont);
                    }

                    return style;
                });

                page.Content().Column(content =>
                {
                    // ---- entête de document (page 1 uniquement)
                    content.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Row(logoRow =>
                            {
                                if (doc.Company.Logo is { Length: > 0 })
                                {
                                    logoRow.ConstantItem(56).Height(56).Image(doc.Company.Logo).FitArea();
                                }
                                else
                                {
                                    logoRow.ConstantItem(0);
                                }

                                logoRow.RelativeItem().PaddingLeft(12).Column(name =>
                                {
                                    name.Item().Text(doc.Company.Name)
                                        .FontSize(headerSize).Bold().FontColor(accent);
                                    if (!string.IsNullOrWhiteSpace(doc.Company.NIF))
                                    {
                                        name.Item().PaddingTop(2).Text(CompanyFiscalLine(doc.Company)).FontSize(7.5f).FontColor("#64748B");
                                    }

                                    if (!string.IsNullOrWhiteSpace(doc.Company.RIB) || !string.IsNullOrWhiteSpace(doc.Company.CCP))
                                    {
                                        name.Item().PaddingTop(1).Text(CompanyBankLine(doc.Company)).FontSize(7.5f).FontColor("#64748B");
                                    }
                                });
                            });

                            left.Item().PaddingTop(4).Text(CompanyContactLine(doc.Company)).FontSize(8).FontColor("#475569");
                        });

                        row.ConstantItem(230).Column(right =>
                        {
                            right.Item().Padding(8).Border(1).BorderColor(accent).Background(accent).AlignCenter()
                                .Text(doc.Title).FontSize(headerSize).Bold().FontColor("#FFFFFF");
                            right.Item().PaddingTop(4).Table(t =>
                            {
                                t.ColumnsDefinition(c => { c.ConstantColumn(96); c.RelativeColumn(); });
                                Meta(t, s.Number, doc.InvoiceNumber, accent, boldValue: true);
                                Meta(t, s.IssueDate, Date(doc.IssueDate), accent);
                                if (doc.DueDate.HasValue)
                                {
                                    Meta(t, s.DueDate, Date(doc.DueDate.Value), accent);
                                }

                                Meta(t, s.PaymentMethodLabel, doc.PaymentMethod, accent);
                                if (!string.IsNullOrWhiteSpace(doc.OrderReference))
                                {
                                    Meta(t, s.OrderReference, doc.OrderReference, accent);
                                }
                            });
                        });
                    });

                    content.Item().PaddingTop(8).LineHorizontal(1.2f).LineColor(accent);

                    // ---- parties
                    content.Item().PaddingTop(12).Row(row =>
                    {
                        row.RelativeItem().Column(client =>
                        {
                            client.Item().Text(s.BillTo).FontSize(8).Bold().FontColor(accent);
                            client.Item().PaddingTop(3).Text(doc.Client.Name).FontSize(baseSize).Bold();
                            if (!string.IsNullOrWhiteSpace(doc.Client.Address))
                            {
                                client.Item().PaddingTop(1).Text(doc.Client.Address).FontSize(baseSize);
                            }

                            var clientInfo = PartyFiscalLine(doc.Client);
                            if (!string.IsNullOrWhiteSpace(clientInfo))
                            {
                                client.Item().PaddingTop(2).Text(clientInfo).FontSize(baseSize).FontColor("#64748B");
                            }

                            var contact = PartyContactLine(doc.Client);
                            if (!string.IsNullOrWhiteSpace(contact))
                            {
                                client.Item().PaddingTop(1).Text(contact).FontSize(baseSize).FontColor("#64748B");
                            }
                        });

                        row.ConstantItem(230).Column(status =>
                        {
                                status.Item().AlignRight().Text($"{s.StatusLabel} : {doc.Status}")
                                    .FontSize(baseSize).Bold().FontColor(doc.StatusColorHex);
                            if (doc.Totals.MontantPaye > 0m)
                            {
                                status.Item().PaddingTop(4).AlignRight().Text($"{s.AmountPaid} : {Money(doc.Totals.MontantPaye)}").FontSize(9);
                                status.Item().PaddingTop(1).AlignRight().Text($"{s.BalanceDue} : {Money(doc.Totals.SoldeRestant)}").FontSize(9).Bold();
                            }
                        });
                    });

                    // ---- lignes
                    content.Item().PaddingTop(14).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(24);
                            c.ConstantColumn(74);
                            c.RelativeColumn();
                            c.ConstantColumn(44);
                            c.ConstantColumn(72);
                            c.ConstantColumn(44);
                            c.ConstantColumn(82);
                            c.ConstantColumn(82);
                        });

                        table.Header(header =>
                        {
                            HeaderCellTcd(header, s.Index, accent);
                            HeaderCellTcd(header, s.Reference, accent);
                            HeaderCellTcd(header, s.Designation, accent);
                            HeaderCellTcd(header, s.Quantity, accent, alignRight: true);
                            HeaderCellTcd(header, s.UnitPrice, accent, alignRight: true);
                            HeaderCellTcd(header, s.Vat, accent, alignRight: true);
                            HeaderCellTcd(header, s.AmountHT, accent, alignRight: true);
                            HeaderCellTcd(header, s.AmountTTC, accent, alignRight: true);
                        });

                        foreach (var line in doc.Lines)
                        {
                            LineCell(table, line.Index.ToString(), bold: true);
                            LineCell(table, line.Reference);
                            LineCell(table, line.Designation);
                            LineCell(table, Qty(line.Quantity), alignRight: true);
                            LineCell(table, Money(line.UnitPriceHT), alignRight: true);
                            LineCell(table, line.VatLabel, alignRight: true);
                            LineCell(table, Money(line.TotalHT), alignRight: true);
                            LineCell(table, Money(line.TotalTTC), alignRight: true);
                        }
                    });

                    // ---- totaux + récap TVA
                    content.Item().EnsureSpace(190).PaddingTop(14).Row(bottom =>
                    {
                        bottom.RelativeItem().Column(tva =>
                        {
                            if (doc.VatBreakdowns.Count > 0)
                            {
                                tva.Item().Text(s.VatSummary).FontSize(8).Bold().FontColor(accent);
                                tva.Item().PaddingTop(3).Table(bt =>
                                {
                                    bt.ColumnsDefinition(c =>
                                    {
                                        c.ConstantColumn(50);
                                        c.ConstantColumn(80);
                                        c.ConstantColumn(80);
                                        c.ConstantColumn(80);
                                    });
                                    HeaderCell(bt, s.Rate, accent);
                                    HeaderCell(bt, s.Base, accent, alignRight: true);
                                    HeaderCell(bt, s.VatAmount, accent, alignRight: true);
                                    HeaderCell(bt, s.Ttc, accent, alignRight: true);
                                    foreach (var b in doc.VatBreakdowns)
                                    {
                                        LineCell(bt, b.Label);
                                        LineCell(bt, Money(b.BaseHT), alignRight: true);
                                        LineCell(bt, Money(b.VatAmount), alignRight: true);
                                        LineCell(bt, Money(b.Ttc), alignRight: true);
                                    }
                                });
                            }
                            else
                            {
                                tva.Item().Text(s.VatSummary).FontSize(8).Bold().FontColor(accent);
                                tva.Item().PaddingTop(3).Table(bt =>
                                {
                                    bt.ColumnsDefinition(c =>
                                    {
                                        c.ConstantColumn(50);
                                        c.ConstantColumn(80);
                                        c.ConstantColumn(80);
                                        c.ConstantColumn(80);
                                    });
                                    HeaderCell(bt, s.Rate, accent);
                                    HeaderCell(bt, s.Base, accent, alignRight: true);
                                    HeaderCell(bt, s.VatAmount, accent, alignRight: true);
                                    HeaderCell(bt, s.Ttc, accent, alignRight: true);
                                    LineCell(bt, "—");
                                    LineCell(bt, Money(doc.Totals.TotalHT), alignRight: true);
                                    LineCell(bt, Money(doc.Totals.TotalTVA), alignRight: true);
                                    LineCell(bt, Money(doc.Totals.TotalTTC), alignRight: true);
                                });
                            }
                        });

                        bottom.ConstantItem(240).Column(total =>
                        {
                            TotalRow(total, s.Subtotal, Money(doc.Totals.TotalHT), accent);
                            if (doc.Totals.RemiseAmount > 0m)
                            {
                                var label = string.IsNullOrWhiteSpace(doc.Totals.RemiseLabel)
                                    ? s.DiscountDetail
                                    : $"{s.DiscountDetail} ({doc.Totals.RemiseLabel})";
                                TotalRow(total, label, "- " + Money(doc.Totals.RemiseAmount), accent);
                            }

                            TotalRow(total, s.TotalVat, Money(doc.Totals.TotalTVA), accent);
                            foreach (var b in doc.VatBreakdowns)
                            {
                                SmallRow(total, $"   {s.Including} {s.Vat} {b.Label}", Money(b.VatAmount));
                            }

                            if (doc.Totals.FraisPort is > 0m)
                            {
                                TotalRow(total, doc.Totals.FraisPortLabel ?? s.Shipping, Money(doc.Totals.FraisPort.Value), accent);
                            }

                            if (doc.Totals.AutresFrais is > 0m)
                            {
                                TotalRow(total, doc.Totals.AutresFraisLabel ?? s.OtherFees, Money(doc.Totals.AutresFrais.Value), accent);
                            }

                            total.Item().PaddingTop(6).Table(gt =>
                            {
                                gt.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(120); });
                                gt.Cell().Element(x => Box(x, accent)).Text(s.TotalTTC).FontColor("#FFFFFF").Bold().FontSize(12);
                                gt.Cell().Element(x => Box(x, accent)).AlignRight().Text(Money(doc.Totals.TotalTTC)).FontColor("#FFFFFF").Bold().FontSize(12);
                            });

                            if (doc.Totals.MontantPaye > 0m)
                            {
                                TotalRow(total, s.AmountPaid, "- " + Money(doc.Totals.MontantPaye), accent);
                                TotalRow(total, s.BalanceDue, Money(doc.Totals.SoldeRestant), accent);
                            }
                        });
                    });

                    // ---- montant en lettres
                    if (!string.IsNullOrWhiteSpace(doc.AmountInWords))
                    {
                        content.Item().EnsureSpace(30).PaddingTop(14).Text(t =>
                        {
                            t.Span($"{s.AmountInWordsLabel} ").Bold().FontSize(baseSize).FontColor("#334155");
                            t.Span(doc.AmountInWords).FontSize(baseSize).FontColor("#334155").Italic();
                        });
                    }

                    // ---- notes
                    var hasNotes = doc.PaymentConditions is { Length: > 0 } || doc.Penalties is { Length: > 0 }
                                   || doc.MentionsSpecifiques is { Length: > 0 } || doc.Notes is { Length: > 0 };
                    if (hasNotes)
                    {
                        content.Item().EnsureSpace(80).PaddingTop(12).Column(notes =>
                        {
                            notes.Item().Text(s.ConditionsAndMentions).FontSize(baseSize).Bold().FontColor(accent);
                            if (doc.PaymentConditions is { Length: > 0 })
                            {
                                notes.Item().PaddingTop(2).Text($"{s.PaymentConditions} : {doc.PaymentConditions}").FontSize(baseSize);
                            }

                            if (doc.Penalties is { Length: > 0 })
                            {
                                notes.Item().PaddingTop(1).Text($"{s.LatePenalties} : {doc.Penalties}").FontSize(baseSize);
                            }

                            if (doc.MentionsSpecifiques is { Length: > 0 })
                            {
                                notes.Item().PaddingTop(1).Text(doc.MentionsSpecifiques).FontSize(baseSize);
                            }

                            if (doc.Notes is { Length: > 0 })
                            {
                                notes.Item().PaddingTop(1).Text($"{s.Notes} : {doc.Notes}").FontSize(baseSize);
                            }
                        });
                    }
                });

                page.Footer().DefaultTextStyle(x => x.FontSize(footerSize)).AlignRight().Text(t =>
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

    private static string Date(DateTime d) => d.ToString("dd/MM/yyyy", Fr);
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

    private static IContainer Box(IContainer container, string color) =>
        container.Background(color).Padding(6);

    private static IContainer Cell(IContainer container) =>
        container.Border(0.5f).BorderColor("#E2E8F0").Padding(4);

    private static void TotalRow(ColumnDescriptor column, string label, string value, string accent)
    {
        column.Item().PaddingTop(2).Table(t =>
        {
            t.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(120); });
            t.Cell().Element(x => x.PaddingVertical(1)).Text(label).FontSize(8);
            t.Cell().Element(x => x.PaddingVertical(1)).AlignRight().Text(value).FontSize(8).SemiBold();
        });
    }

    private static void SmallRow(ColumnDescriptor column, string label, string value)
    {
        column.Item().Table(t =>
        {
            t.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(120); });
            t.Cell().Element(x => x.PaddingVertical(0.5f)).Text(label).FontSize(7).FontColor("#64748B");
            t.Cell().Element(x => x.PaddingVertical(0.5f)).AlignRight().Text(value).FontSize(7).FontColor("#64748B");
        });
    }

    /// <summary>
    /// Résout une famille de police sûre pour QuestPDF. Renvoie <c>null</c> si la police n'est pas
    /// dans la liste blanche ou si son enregistrement depuis C:\Windows\Fonts échoue, afin de ne
    /// jamais lever d'exception à la génération (une police non enregistrée fait planter QuestPDF).
    /// </summary>
    private static string? ResolvePdfFont(string? family)
    {
        if (string.IsNullOrWhiteSpace(family) || !SafeFontFamilies.Contains(family))
        {
            return null;
        }

        if (RegisteredFonts.Contains(family))
        {
            return family;
        }

        if (FontFiles.TryGetValue(family, out var fileName))
        {
            var fontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            var path = Path.Combine(fontsDir, fileName);
            if (File.Exists(path))
            {
                try
                {
                    using var stream = File.OpenRead(path);
                    FontManager.RegisterFont(stream);
                    RegisteredFonts.Add(family);
                    return family;
                }
                catch
                {
                    // enregistrement impossible : on garde la police par défaut du renderer
                }
            }
        }

        return null;
    }
}
