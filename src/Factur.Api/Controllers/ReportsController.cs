using Factur.Application.DTOs;
using Factur.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Factur.Api.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly IExportService _exportService;

    public ReportsController(IReportService reportService, IExportService exportService)
    {
        _reportService = reportService;
        _exportService = exportService;
    }

    [HttpGet("monthly")]
    public async Task<ActionResult<MonthlyReportDto>> Monthly([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        return Ok(await _reportService.GetMonthlyReportAsync(year, month, ct));
    }

    [HttpGet("monthly/invoices")]
    public async Task<ActionResult<PagedResult<InvoiceSummaryDto>>> MonthlyInvoices([FromQuery] int year, [FromQuery] int month, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Ok(await _reportService.GetMonthlyInvoicesPagedAsync(year, month, page, pageSize, ct));
    }

    [HttpGet("monthly/export/pdf")]
    public async Task<IActionResult> MonthlyPdf([FromQuery] int year, [FromQuery] int month, [FromQuery] string? lang, CancellationToken ct)
    {
        var bytes = await _exportService.ExportMonthlyReportPdfAsync(year, month, lang, ct);
        return File(bytes, "application/pdf", $"rapport-mensuel-{year}-{month:D2}.pdf");
    }

    [HttpGet("monthly/export/xlsx")]
    public async Task<IActionResult> MonthlyExcel([FromQuery] int year, [FromQuery] int month, [FromQuery] string? lang, CancellationToken ct)
    {
        var bytes = await _exportService.ExportMonthlyReportExcelAsync(year, month, lang, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"rapport-mensuel-{year}-{month:D2}.xlsx");
    }

    [HttpGet("tva")]
    public async Task<ActionResult<TVAReportDto>> Tva([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        return Ok(await _reportService.GetTVAReportAsync(from, to, ct));
    }

    [HttpGet("tva/export/pdf")]
    public async Task<IActionResult> TvaPdf([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? lang, CancellationToken ct)
    {
        var bytes = await _exportService.ExportTvaReportPdfAsync(from, to, lang, ct);
        return File(bytes, "application/pdf", $"declaration-tva-{DateTime.UtcNow:yyyyMMdd}.pdf");
    }

    [HttpGet("tva/export/xlsx")]
    public async Task<IActionResult> TvaExcel([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? lang, CancellationToken ct)
    {
        var bytes = await _exportService.ExportTvaReportExcelAsync(from, to, lang, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"declaration-tva-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    [HttpGet("unpaid")]
    public async Task<ActionResult<IReadOnlyList<InvoiceSummaryDto>>> Unpaid(CancellationToken ct)
    {
        return Ok(await _reportService.GetUnpaidInvoicesAsync(ct: ct));
    }

    [HttpGet("unpaid/paged")]
    public async Task<ActionResult<PagedResult<InvoiceSummaryDto>>> UnpaidPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Ok(await _reportService.GetUnpaidPagedAsync(page, pageSize, ct));
    }

    [HttpGet("unpaid/export/pdf")]
    public async Task<IActionResult> UnpaidPdf([FromQuery] string? lang, CancellationToken ct)
    {
        var bytes = await _exportService.ExportUnpaidPdfAsync(lang, ct);
        return File(bytes, "application/pdf", $"impayes-{DateTime.UtcNow:yyyyMMdd}.pdf");
    }

    [HttpGet("unpaid/export/xlsx")]
    public async Task<IActionResult> UnpaidExcel([FromQuery] string? lang, CancellationToken ct)
    {
        var bytes = await _exportService.ExportUnpaidExcelAsync(lang, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"impayes-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    [HttpGet("top-clients")]
    public async Task<ActionResult<IReadOnlyList<TopClientDto>>> TopClients([FromQuery] int count = 10, CancellationToken ct = default)
    {
        return Ok(await _reportService.GetTopClientsAsync(count, ct: ct));
    }

    [HttpGet("top-clients/export/pdf")]
    public async Task<IActionResult> TopClientsPdf([FromQuery] int count = 10, [FromQuery] string? lang = null, CancellationToken ct = default)
    {
        var bytes = await _exportService.ExportTopClientsPdfAsync(count, lang, ct);
        return File(bytes, "application/pdf", $"meilleurs-clients-{DateTime.UtcNow:yyyyMMdd}.pdf");
    }

    [HttpGet("top-clients/export/xlsx")]
    public async Task<IActionResult> TopClientsExcel([FromQuery] int count = 10, [FromQuery] string? lang = null, CancellationToken ct = default)
    {
        var bytes = await _exportService.ExportTopClientsExcelAsync(count, lang, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"meilleurs-clients-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    [HttpGet("yearly")]
    public async Task<ActionResult<IReadOnlyList<YearlyPointDto>>> Yearly([FromQuery] int year, CancellationToken ct)
    {
        return Ok(await _reportService.GetYearlyTotalsAsync(year, ct));
    }
}

[ApiController]
[Route("api/audit")]
public class AuditController : ControllerBase
{
    private readonly IAuditService _auditService;

    public AuditController(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuditLogDto>>> Get([FromQuery] string? entityType, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int limit = 200, CancellationToken ct = default)
    {
        return Ok(await _auditService.GetAsync(entityType, from, to, limit, ct));
    }
}
