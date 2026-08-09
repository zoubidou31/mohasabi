using System.Globalization;
using System.Text;
using Factur.Application.Common.Exceptions;
using Factur.Application.DTOs;
using Factur.Application.Interfaces;
using Factur.Domain.Entities;
using Factur.Domain.Enums;
using Factur.Infrastructure.Export;
using Factur.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QuestPDF.Infrastructure;

namespace Factur.Infrastructure.Services;

public class ExportService : IExportService
{
    private readonly ApplicationDbContext _context;
    private readonly IReportService _reportService;
    private readonly IOptions<StorageOptions> _storage;

    public ExportService(
        ApplicationDbContext context,
        IReportService reportService,
        IOptions<StorageOptions> storage)
    {
        _context = context;
        _reportService = reportService;
        _storage = storage;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    // ---------------------------------------------------------------- chargement

    private async Task<Invoice> LoadInvoiceAsync(Guid invoiceId, CancellationToken ct)
    {
        return await _context.Invoices.AsNoTracking()
            .Include(i => i.Client)
            .Include(i => i.Company)
            .Include(i => i.Lines)
            .Include(i => i.TVABreakdowns)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct)
            ?? throw new NotFoundException("Facture introuvable.");
    }

    private async Task<CompanyBlock> LoadCompanyBlockAsync(CancellationToken ct)
    {
        var company = await _context.Companies.AsNoTracking().OrderBy(c => c.CreatedDate).FirstOrDefaultAsync(ct);
        return new CompanyBlock
        {
            Name = company?.CompanyName ?? string.Empty,
            Address = ExportDocumentFactory.BuildCompanyAddress(company ?? new Company()),
            Phone = company?.Phone ?? string.Empty,
            Email = company?.Email ?? string.Empty,
            NIF = company?.NIF ?? string.Empty,
            NIS = company?.NIS ?? string.Empty,
            RC = company?.RC ?? string.Empty,
            ART = company?.ART ?? string.Empty,
            RIB = company?.RIB,
            CCP = company?.CCP,
            BankName = company?.BankName,
            Logo = LoadLogo(company),
        };
    }

    private byte[]? LoadLogo(Company? company)
    {
        if (company is null || string.IsNullOrWhiteSpace(company.LogoPath))
        {
            return null;
        }

        var fileName = Path.GetFileName(company.LogoPath.Replace('\\', '/'));
        var fullPath = Path.Combine(StoragePaths.ResolveUploads(_storage.Value), fileName);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        try
        {
            return File.ReadAllBytes(fullPath);
        }
        catch
        {
            return null;
        }
    }

    private async Task<ExportDocument> BuildDocumentAsync(Guid invoiceId, DocumentStrings strings, CancellationToken ct)
    {
        var invoice = await LoadInvoiceAsync(invoiceId, ct);
        return ExportDocumentFactory.FromInvoice(invoice, strings, LoadLogo(invoice.Company));
    }

    // ---------------------------------------------------------------- factures

    public async Task<byte[]> ExportPdfAsync(Guid invoiceId, string? lang = null, CancellationToken ct = default)
    {
        var strings = DocumentStrings.For(lang);
        var doc = await BuildDocumentAsync(invoiceId, strings, ct);
        return InvoicePdfRenderer.Render(doc);
    }

    public async Task<byte[]> ExportExcelAsync(Guid invoiceId, string? lang = null, CancellationToken ct = default)
    {
        var strings = DocumentStrings.For(lang);
        var doc = await BuildDocumentAsync(invoiceId, strings, ct);
        return InvoiceExcelRenderer.Render(doc);
    }

    public async Task<byte[]> ExportWordAsync(Guid invoiceId, string? lang = null, CancellationToken ct = default)
    {
        var strings = DocumentStrings.For(lang);
        var doc = await BuildDocumentAsync(invoiceId, strings, ct);
        return InvoiceWordRenderer.Render(doc);
    }

    public byte[] ExportCsv(InvoiceDto invoice, string? lang = null)
    {
        var strings = DocumentStrings.For(lang);
        var fr = CultureInfo.GetCultureInfo("fr-FR");
        var sb = new StringBuilder();
        sb.AppendLine($"{strings.Reference};{strings.Designation};{strings.Quantity};{strings.UnitPrice};{strings.Vat};{strings.AmountHT};{strings.VatAmount};{strings.AmountTTC}");
        foreach (var line in invoice.Lines)
        {
            sb.AppendLine(string.Join(';',
                Csv(line.Reference),
                Csv(line.Description),
                line.Quantity.ToString("0.##", fr),
                line.UnitPriceHT.ToString("0.00", fr),
                strings.TvaLabel(line.TVARate),
                line.TotalHT.ToString("0.00", fr),
                line.TVAAmount.ToString("0.00", fr),
                line.TotalTTC.ToString("0.00", fr)));
        }

        sb.AppendLine();
        sb.AppendLine($"TotalHT;{invoice.TotalHT.ToString("0.00", fr)}");
        sb.AppendLine($"TotalTVA;{invoice.TotalTVA.ToString("0.00", fr)}");
        sb.AppendLine($"TotalTTC;{invoice.TotalTTC.ToString("0.00", fr)}");

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string Csv(string? value) => value is null
        ? string.Empty
        : FormulaSanitizer.Sanitize($"\"{value.Replace("\"", "\"\"")}\"");

    public async Task<byte[]> ExportInvoicesExcelAsync(IEnumerable<InvoiceSummaryDto> invoices, string? lang = null, CancellationToken ct = default)
    {
        var strings = DocumentStrings.For(lang);
        var company = await LoadCompanyBlockAsync(ct);
        return InvoicesListExcelRenderer.Render(invoices, strings, company);
    }

    // ---------------------------------------------------------------- rapports

    public async Task<byte[]> ExportMonthlyReportPdfAsync(int year, int month, string? lang = null, CancellationToken ct = default)
    {
        var strings = DocumentStrings.For(lang);
        var report = await _reportService.GetMonthlyReportAsync(year, month, ct);
        var company = await LoadCompanyBlockAsync(ct);

        var data = new ReportData
        {
            Title = strings.MonthlyReport,
            Subtitle = $"{strings.MonthName(month)} {year}",
            Headers = new[]
            {
                strings.InvoiceNumber, strings.InvoiceDate, strings.ClientName, strings.Type, strings.Status,
                strings.TotalHT, strings.TotalVat, strings.TotalTTC, strings.Payee, strings.Solde,
            },
            Rows = report.Invoices.Select(i => (IReadOnlyList<object?>)new object?[]
            {
                i.InvoiceNumber,
                i.InvoiceDate,
                i.ClientName,
                TypeLabel(i.InvoiceType, strings),
                strings.StatusText(i.Status),
                i.TotalHT,
                i.TotalTVA,
                i.TotalTTC,
                i.MontantPaye,
                i.SoldeRestant,
            }).ToList(),
            Totals = new decimal?[] { null, null, null, null, null, report.TotalHT, report.TotalTVA, report.TotalTTC, report.TotalCollected, report.Outstanding },
            Summary = new List<(string, string)>
            {
                (strings.InvoiceCount, report.InvoiceCount.ToString()),
                (strings.TotalHT, Money(report.TotalHT)),
                (strings.TotalVat, Money(report.TotalTVA)),
                (strings.TotalTTC, Money(report.TotalTTC)),
                (strings.Collected, Money(report.TotalCollected)),
                (strings.Outstanding, Money(report.Outstanding)),
            },
        };

        return ReportPdfRenderer.Render(data, Header(company, strings));
    }

    public async Task<byte[]> ExportMonthlyReportExcelAsync(int year, int month, string? lang = null, CancellationToken ct = default)
    {
        var strings = DocumentStrings.For(lang);
        var report = await _reportService.GetMonthlyReportAsync(year, month, ct);
        var company = await LoadCompanyBlockAsync(ct);

        var data = new ReportData
        {
            Title = strings.MonthlyReport,
            Subtitle = $"{strings.MonthName(month)} {year}",
            Headers = new[]
            {
                strings.InvoiceNumber, strings.InvoiceDate, strings.ClientName, strings.Type, strings.Status,
                strings.TotalHT, strings.TotalVat, strings.TotalTTC, strings.Payee, strings.Solde,
            },
            Rows = report.Invoices.Select(i => (IReadOnlyList<object?>)new object?[]
            {
                i.InvoiceNumber,
                i.InvoiceDate,
                i.ClientName,
                TypeLabel(i.InvoiceType, strings),
                strings.StatusText(i.Status),
                i.TotalHT,
                i.TotalTVA,
                i.TotalTTC,
                i.MontantPaye,
                i.SoldeRestant,
            }).ToList(),
            Totals = new decimal?[] { null, null, null, null, null, report.TotalHT, report.TotalTVA, report.TotalTTC, report.TotalCollected, report.Outstanding },
            Summary = new List<(string, string)>
            {
                (strings.InvoiceCount, report.InvoiceCount.ToString()),
                (strings.TotalHT, Money(report.TotalHT)),
                (strings.TotalVat, Money(report.TotalTVA)),
                (strings.TotalTTC, Money(report.TotalTTC)),
                (strings.Collected, Money(report.TotalCollected)),
                (strings.Outstanding, Money(report.Outstanding)),
            },
        };

        return ReportExcelRenderer.Render(data, strings, company);
    }

    public async Task<byte[]> ExportTvaReportPdfAsync(DateTime? from, DateTime? to, string? lang = null, CancellationToken ct = default)
    {
        var strings = DocumentStrings.For(lang);
        var company = await LoadCompanyBlockAsync(ct);
        var (rows, totals) = await LoadTvaRowsAsync(from, to, strings, ct);

        var data = new ReportData
        {
            Title = strings.TvaDeclaration,
            Subtitle = PeriodText(from, to, strings),
            Headers = new[] { strings.Rate, strings.Base, strings.VatAmount, strings.Ttc },
            Rows = rows.Select(r => (IReadOnlyList<object?>)new object?[] { r.Item1, r.Item2, r.Item3, r.Item4 }).ToList(),
            Totals = new decimal?[] { null, totals.TotalHT, totals.TotalTVA, totals.TotalTTC },
        };

        return ReportPdfRenderer.Render(data, Header(company, strings));
    }

    public async Task<byte[]> ExportTvaReportExcelAsync(DateTime? from, DateTime? to, string? lang = null, CancellationToken ct = default)
    {
        var strings = DocumentStrings.For(lang);
        var company = await LoadCompanyBlockAsync(ct);
        var (rows, totals) = await LoadTvaRowsAsync(from, to, strings, ct);

        var data = new ReportData
        {
            Title = strings.TvaDeclaration,
            Subtitle = PeriodText(from, to, strings),
            Headers = new[] { strings.Rate, strings.Base, strings.VatAmount, strings.Ttc },
            Rows = rows.Select(r => (IReadOnlyList<object?>)new object?[] { r.Item1, r.Item2, r.Item3, r.Item4 }).ToList(),
            Totals = new decimal?[] { null, totals.TotalHT, totals.TotalTVA, totals.TotalTTC },
        };

        return ReportExcelRenderer.Render(data, strings, company);
    }

    public async Task<byte[]> ExportUnpaidPdfAsync(string? lang = null, CancellationToken ct = default)
    {
        var strings = DocumentStrings.For(lang);
        var list = await _reportService.GetUnpaidInvoicesAsync(ct: ct);
        var company = await LoadCompanyBlockAsync(ct);

        var data = new ReportData
        {
            Title = strings.UnpaidList,
            Headers = new[] { strings.InvoiceNumber, strings.InvoiceDate, strings.ClientName, strings.DueDateShort, strings.Solde },
            Rows = list.Select(i => (IReadOnlyList<object?>)new object?[]
            {
                i.InvoiceNumber, i.InvoiceDate, i.ClientName, i.DueDate, i.SoldeRestant,
            }).ToList(),
            Totals = new decimal?[] { null, null, null, null, list.Sum(i => i.SoldeRestant) },
            Summary = new List<(string, string)>
            {
                (strings.InvoiceCount, list.Count.ToString()),
                (strings.Outstanding, Money(list.Sum(i => i.SoldeRestant))),
            },
        };

        return ReportPdfRenderer.Render(data, Header(company, strings));
    }

    public async Task<byte[]> ExportUnpaidExcelAsync(string? lang = null, CancellationToken ct = default)
    {
        var strings = DocumentStrings.For(lang);
        var list = await _reportService.GetUnpaidInvoicesAsync(ct: ct);
        var company = await LoadCompanyBlockAsync(ct);

        var data = new ReportData
        {
            Title = strings.UnpaidList,
            Headers = new[] { strings.InvoiceNumber, strings.InvoiceDate, strings.ClientName, strings.DueDateShort, strings.Solde },
            Rows = list.Select(i => (IReadOnlyList<object?>)new object?[]
            {
                i.InvoiceNumber, i.InvoiceDate, i.ClientName, i.DueDate, i.SoldeRestant,
            }).ToList(),
            Totals = new decimal?[] { null, null, null, null, list.Sum(i => i.SoldeRestant) },
            Summary = new List<(string, string)>
            {
                (strings.InvoiceCount, list.Count.ToString()),
                (strings.Outstanding, Money(list.Sum(i => i.SoldeRestant))),
            },
        };

        return ReportExcelRenderer.Render(data, strings, company);
    }

    public async Task<byte[]> ExportTopClientsPdfAsync(int count = 10, string? lang = null, CancellationToken ct = default)
    {
        var strings = DocumentStrings.For(lang);
        var list = await _reportService.GetTopClientsAsync(count, ct: ct);
        var company = await LoadCompanyBlockAsync(ct);

        var data = new ReportData
        {
            Title = strings.TopClients,
            Headers = new[] { strings.Index, strings.ClientName, strings.InvoiceCount, strings.TotalTTC },
            Rows = list.Select((c, i) => (IReadOnlyList<object?>)new object?[]
            {
                (i + 1).ToString(), c.ClientName, c.InvoiceCount.ToString(), c.TotalTTC,
            }).ToList(),
            Totals = new decimal?[] { null, null, null, list.Sum(c => c.TotalTTC) },
        };

        return ReportPdfRenderer.Render(data, Header(company, strings));
    }

    public async Task<byte[]> ExportTopClientsExcelAsync(int count = 10, string? lang = null, CancellationToken ct = default)
    {
        var strings = DocumentStrings.For(lang);
        var list = await _reportService.GetTopClientsAsync(count, ct: ct);
        var company = await LoadCompanyBlockAsync(ct);

        var data = new ReportData
        {
            Title = strings.TopClients,
            Headers = new[] { strings.Index, strings.ClientName, strings.InvoiceCount, strings.TotalTTC },
            Rows = list.Select((c, i) => (IReadOnlyList<object?>)new object?[]
            {
                (i + 1).ToString(), c.ClientName, c.InvoiceCount.ToString(), c.TotalTTC,
            }).ToList(),
            Totals = new decimal?[] { null, null, null, list.Sum(c => c.TotalTTC) },
        };

        return ReportExcelRenderer.Render(data, strings, company);
    }

    // ---------------------------------------------------------------- helpers

    private async Task<(List<(string, decimal, decimal, decimal)> Rows, (decimal TotalHT, decimal TotalTVA, decimal TotalTTC))> LoadTvaRowsAsync(DateTime? from, DateTime? to, DocumentStrings strings, CancellationToken ct)
    {
        var start = from ?? DateTime.UtcNow.AddMonths(-1);
        var end = to ?? DateTime.UtcNow;

        var grouped = await _context.TVABreakdowns.AsNoTracking()
            .Where(b => b.Invoice!.InvoiceDate >= start.Date && b.Invoice.InvoiceDate <= end.Date
                        && b.Invoice.Status != InvoiceStatus.Annulee && b.Invoice.Status != InvoiceStatus.Brouillon)
            .GroupBy(b => b.TVARate)
            .Select(g => new { Rate = g.Key, HT = g.Sum(b => b.TotalHT), TVA = g.Sum(b => b.TVAAmount), TTC = g.Sum(b => b.TotalTTC) })
            .ToListAsync(ct);

        var ordered = grouped.OrderByDescending(x => x.TVA).ToList();
        var rows = ordered.Select(g => (strings.TvaLabel(g.Rate), g.HT, g.TVA, g.TTC)).ToList();
        return (rows, (rows.Sum(r => r.Item2), rows.Sum(r => r.Item3), rows.Sum(r => r.Item4)));
    }

    private static string PeriodText(DateTime? from, DateTime? to, DocumentStrings strings)
    {
        var fr = CultureInfo.GetCultureInfo("fr-FR");
        var fromText = from?.ToString("dd/MM/yyyy", fr) ?? "—";
        var toText = to?.ToString("dd/MM/yyyy", fr) ?? "—";
        return $"{strings.Period} : {fromText} → {toText}";
    }

    private static string TypeLabel(InvoiceType type, DocumentStrings strings) => type switch
    {
        InvoiceType.ProForma => strings.ProForma,
        InvoiceType.Avoir => strings.Avoir,
        _ => strings.Facture,
    };

    private static string Money(decimal value) => $"{value.ToString("N2", CultureInfo.GetCultureInfo("fr-FR"))} DA";

    private static ExportDocument Header(CompanyBlock company, DocumentStrings strings) => new()
    {
        Strings = strings,
        Company = company,
    };
}
