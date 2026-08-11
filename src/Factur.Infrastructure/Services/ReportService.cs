using Factur.Application.Common.Mapping;
using Factur.Application.DTOs;
using Factur.Application.Interfaces;
using Factur.Domain.Enums;
using Factur.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Factur.Infrastructure.Services;

public class ReportService : IReportService
{
    private readonly ApplicationDbContext _context;

    public ReportService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<MonthlyReportDto> GetMonthlyReportAsync(int year, int month, CancellationToken ct = default)
    {
        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var invoices = await _context.Invoices.AsNoTracking()
            .Include(i => i.Client)
            .Include(i => i.TVABreakdowns)
            .Where(i => i.InvoiceDate >= from && i.InvoiceDate <= to && i.Status != InvoiceStatus.Annulee)
            .OrderBy(i => i.InvoiceNumber)
            .ToListAsync(ct);

        var collected = await _context.Payments.AsNoTracking()
            .Where(p => p.PaymentDate >= from && p.PaymentDate <= to)
            .SumAsync(p => p.Amount, ct);

        var active = invoices.Where(i => i.Status != InvoiceStatus.Brouillon).ToList();

        var tvaByRate = active
            .SelectMany(i => i.TVABreakdowns)
            .GroupBy(b => b.TVARate)
            .Select(g => new TVAReportDto
            {
                TVARate = g.Key.Label(),
                TotalHT = g.Sum(b => b.TotalHT),
                TVAAmount = g.Sum(b => b.TVAAmount),
                TotalTTC = g.Sum(b => b.TotalTTC),
            })
            .OrderByDescending(x => x.TVAAmount)
            .ToList();

        return new MonthlyReportDto
        {
            Year = year,
            Month = month,
            InvoiceCount = active.Count,
            TotalHT = active.Sum(i => i.TotalHT),
            TotalTVA = active.Sum(i => i.TotalTVA),
            TotalTTC = active.Sum(i => i.TotalTTC),
            TotalCollected = collected,
            Outstanding = active.Sum(i => i.SoldeRestant),
            TVAByRate = tvaByRate,
            Invoices = invoices.Select(i => i.ToSummaryDto()).ToList(),
        };
    }

    public async Task<PagedResult<InvoiceSummaryDto>> GetMonthlyInvoicesPagedAsync(int year, int month, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var q = _context.Invoices.AsNoTracking()
            .Include(i => i.Client)
            .Where(i => i.InvoiceDate >= from && i.InvoiceDate <= to && i.Status != InvoiceStatus.Annulee)
            .OrderBy(i => i.InvoiceNumber);

        var safePage = Math.Max(1, page);
        var safeSize = Math.Clamp(pageSize, 1, 200);

        var totalCount = await q.CountAsync(ct);
        var items = await q
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .ToListAsync(ct);

        return new PagedResult<InvoiceSummaryDto>
        {
            Items = items.Select(i => i.ToSummaryDto()).ToList(),
            TotalCount = totalCount,
            Page = safePage,
            PageSize = safeSize,
        };
    }

    public async Task<TVAReportDto> GetTVAReportAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var start = from ?? DateTime.UtcNow.AddMonths(-1);
        var end = to ?? DateTime.UtcNow;

        var breakdowns = await _context.TVABreakdowns.AsNoTracking()
            .Where(b => b.Invoice!.InvoiceDate >= start.Date && b.Invoice.InvoiceDate <= end.Date
                        && b.Invoice.Status != InvoiceStatus.Annulee && b.Invoice.Status != InvoiceStatus.Brouillon)
            .GroupBy(b => b.TVARate)
            .Select(g => new { Rate = g.Key, HT = g.Sum(b => b.TotalHT), TVA = g.Sum(b => b.TVAAmount), TTC = g.Sum(b => b.TotalTTC) })
            .ToListAsync(ct);

        var result = new TVAReportDto
        {
            TVARate = "Total",
            TotalHT = breakdowns.Sum(x => x.HT),
            TVAAmount = breakdowns.Sum(x => x.TVA),
            TotalTTC = breakdowns.Sum(x => x.TTC),
        };

        return result;
    }

    public async Task<IReadOnlyList<InvoiceSummaryDto>> GetUnpaidInvoicesAsync(DateTime? asOf = null, CancellationToken ct = default)
    {
        var date = (asOf ?? DateTime.UtcNow).Date;

        var invoices = await _context.Invoices.AsNoTracking()
            .Include(i => i.Client)
            .Where(i => i.Status != InvoiceStatus.Payee
                        && i.Status != InvoiceStatus.Annulee
                        && i.Status != InvoiceStatus.Brouillon
                        && i.DueDate.HasValue
                        && i.DueDate.Value.Date < date)
            .OrderBy(i => i.DueDate)
            .ToListAsync(ct);

        return invoices.Select(i => i.ToSummaryDto()).ToList();
    }

    public async Task<PagedResult<InvoiceSummaryDto>> GetUnpaidPagedAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var date = DateTime.UtcNow.Date;

        var q = _context.Invoices.AsNoTracking()
            .Include(i => i.Client)
            .Where(i => i.Status != InvoiceStatus.Payee
                        && i.Status != InvoiceStatus.Annulee
                        && i.Status != InvoiceStatus.Brouillon
                        && i.DueDate.HasValue
                        && i.DueDate.Value.Date < date)
            .OrderBy(i => i.DueDate)
            .ThenBy(i => i.InvoiceNumber);

        var safePage = Math.Max(1, page);
        var safeSize = Math.Clamp(pageSize, 1, 200);

        var totalCount = await q.CountAsync(ct);
        var items = await q
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .ToListAsync(ct);

        return new PagedResult<InvoiceSummaryDto>
        {
            Items = items.Select(i => i.ToSummaryDto()).ToList(),
            TotalCount = totalCount,
            Page = safePage,
            PageSize = safeSize,
        };
    }

    public async Task<IReadOnlyList<TopClientDto>> GetTopClientsAsync(int count = 10, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var q = _context.Invoices.AsNoTracking()
            .Where(i => i.Status != InvoiceStatus.Annulee && i.Status != InvoiceStatus.Brouillon);

        if (from.HasValue) q = q.Where(i => i.InvoiceDate >= from.Value.Date);
        if (to.HasValue) q = q.Where(i => i.InvoiceDate <= to.Value.Date);

        return await q
            .GroupBy(i => new { i.ClientId, ClientName = i.Client!.DisplayName })
            .Select(g => new TopClientDto
            {
                ClientId = g.Key.ClientId,
                ClientName = g.Key.ClientName,
                InvoiceCount = g.Count(),
                TotalTTC = g.Sum(i => i.TotalTTC),
            })
            .OrderByDescending(x => (double)x.TotalTTC)
            .Take(count)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<YearlyPointDto>> GetYearlyTotalsAsync(int year, CancellationToken ct = default)
    {
        var points = new List<YearlyPointDto>();

        var data = await _context.Invoices.AsNoTracking()
            .Where(i => i.InvoiceDate.Year == year && i.Status != InvoiceStatus.Annulee && i.Status != InvoiceStatus.Brouillon)
            .GroupBy(i => i.InvoiceDate.Month)
            .Select(g => new { Month = g.Key, TTC = g.Sum(i => i.TotalTTC), TVA = g.Sum(i => i.TotalTVA) })
            .ToListAsync(ct);

        var frenchMonths = new[] { "Janvier", "Février", "Mars", "Avril", "Mai", "Juin", "Juillet", "Août", "Septembre", "Octobre", "Novembre", "Décembre" };

        for (var m = 1; m <= 12; m++)
        {
            var item = data.FirstOrDefault(d => d.Month == m);
            points.Add(new YearlyPointDto
            {
                Label = frenchMonths[m - 1],
                Month = m,
                TotalTTC = item?.TTC ?? 0m,
                TotalTVA = item?.TVA ?? 0m,
            });
        }

        return points;
    }
}
