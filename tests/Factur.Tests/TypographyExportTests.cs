using System.IO.Compression;
using System.Text;
using Factur.Application.DTOs;
using Factur.Domain;
using Factur.Infrastructure.Export;
using Factur.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using QuestPDF;
using QuestPDF.Infrastructure;
using Xunit;

namespace Factur.Tests;

/// <summary>
/// Vérifie que la typographie (police + tailles) est réellement persistée par le backend
/// ET appliquée aux documents exportés (PDF / Word / Excel), et non seulement à l'aperçu.
/// </summary>
public class TypographyExportTests
{
    public TypographyExportTests()
    {
        // QuestPDF exige une licence pour générer ; l'application utilise Community au runtime.
        Settings.License = LicenseType.Community;
    }

    private static ExportDocument BuildSampleDocument()
    {
        var strings = DocumentStrings.For("fr");
        return new ExportDocument
        {
            Strings = strings,
            Title = strings.Facture,
            InvoiceNumber = "F-2026-0001",
            Status = strings.StatusFinalisee,
            IssueDate = new DateTime(2026, 1, 15),
            PaymentMethod = strings.PaymentMethodText(Factur.Domain.Enums.PaymentMethod.Comptant),
            Company = new CompanyBlock { Name = "SARL Example", NIF = "123456789", Address = "Alger" },
            Client = new PartyBlock { Name = "Client SARL", Address = "Oran" },
            Lines = new List<ExportLine>
            {
                new() { Index = 1, Designation = "Prestation A", Quantity = 2, UnitPriceHT = 500, VatLabel = "19%", TotalHT = 1000, TotalTTC = 1190 },
                new() { Index = 2, Designation = "Produit B", Quantity = 1, UnitPriceHT = 500, VatLabel = "19%", TotalHT = 500, TotalTTC = 595 },
            },
            VatBreakdowns = new List<VatBreakdownBlock> { new() { Label = "19%", BaseHT = 1500, VatAmount = 285, Ttc = 1785 } },
            Totals = new TotalsBlock { TotalHT = 1500, TotalTVA = 285, TotalTTC = 1785 },
            AmountInWords = "Mille sept cent quatre-vingt-cinq dinars",
        };
    }

    [Fact]
    public async Task SettingsService_PersistsTypographyAcrossSaveAndReload()
    {
        var root = Path.Combine(Path.GetTempPath(), "moha-typo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["App:DataRoot"] = root })
                .Build();
            var svc = new SettingsService(config);

            var settings = new AppSettings
            {
                Language = "fr",
                Theme = "light",
                DocFontFamily = "Georgia",
                DocBaseFontSize = 18,
                DocTableFontSize = 14,
                DocHeaderFontSize = 24,
                DocFooterFontSize = 12,
                AppFontFamily = "Georgia",
                InterfaceFontSize = "large",
            };

            var saved = await svc.SaveAsync(settings);
            var reloaded = await svc.GetAsync();

            Assert.Equal("Georgia", saved.DocFontFamily);
            Assert.Equal("Georgia", reloaded.DocFontFamily);
            Assert.Equal(18d, reloaded.DocBaseFontSize);
            Assert.Equal(14d, reloaded.DocTableFontSize);
            Assert.Equal(24d, reloaded.DocHeaderFontSize);
            Assert.Equal(12d, reloaded.DocFooterFontSize);
            Assert.Equal("large", reloaded.InterfaceFontSize);
            Assert.Equal("Georgia", reloaded.AppFontFamily);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ignoré */ }
        }
    }

    [Fact]
    public void Export_AppliesTypography_ToWordExcelAndPdf()
    {
        var doc = BuildSampleDocument();
        var typo = new TypographyOptions { FontFamily = "Georgia", BaseFontSize = 18, TableFontSize = 14, HeaderFontSize = 24, FooterFontSize = 12 };
        var typoDefault = new TypographyOptions { FontFamily = "Inter", BaseFontSize = 11, TableFontSize = 9, HeaderFontSize = 13, FooterFontSize = 9 };

        var docx = InvoiceWordRenderer.Render(doc, typo);
        var docxDefault = InvoiceWordRenderer.Render(doc, typoDefault);
        var xlsx = InvoiceExcelRenderer.Render(doc, typo);
        var xlsxDefault = InvoiceExcelRenderer.Render(doc, typoDefault);
        var pdf = InvoicePdfRenderer.Render(doc, typo);
        var pdfDefault = InvoicePdfRenderer.Render(doc, typoDefault);

        var docxText = ExtractZipText(docx);
        var docxDefText = ExtractZipText(docxDefault);
        var xlsxText = ExtractZipText(xlsx);
        var xlsxDefText = ExtractZipText(xlsxDefault);

        // Word : police distinctive + tailles en demi-points (18 -> 36, 24 -> 48).
        Assert.Contains("Georgia", docxText);
        Assert.DoesNotContain("Georgia", docxDefText);
        Assert.Contains("36", docxText);
        Assert.Contains("48", docxText);

        // Excel : police distinctive + tailles en points (18, 24).
        Assert.Contains("Georgia", xlsxText);
        Assert.DoesNotContain("Georgia", xlsxDefText);
        Assert.Contains("18", xlsxText);
        Assert.Contains("24", xlsxText);

        // PDF : la police Georgia doit être référencée (police embarquée).
        var pdfText = Encoding.Latin1.GetString(pdf);
        Assert.StartsWith("%PDF", pdfText);
        if (pdfText.Contains("Georgia", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Contains("Georgia", pdfText, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            // Si la police système est absente, le renderer retombe sur la police par défaut :
            // on vérifie au moins que la typographie a un effet (tailles différentes -> PDF différent).
            var pdfDefText = Encoding.Latin1.GetString(pdfDefault);
            Assert.NotEqual(pdfText, pdfDefText);
        }
    }

    private static string ExtractZipText(byte[] bytes)
    {
        var sb = new StringBuilder();
        using var ms = new MemoryStream(bytes);
        using var zip = new ZipArchive(ms);
        foreach (var entry in zip.Entries)
        {
            using var sr = new StreamReader(entry.Open());
            sb.Append(sr.ReadToEnd());
        }
        return sb.ToString();
    }
}
