using Factur.Application.DTOs;
using Factur.Application.Interfaces;
using Factur.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Factur.Api.Controllers;

[ApiController]
[Route("api/invoices")]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;
    private readonly IExportService _exportService;
    private readonly IEmailService _emailService;

    public InvoicesController(IInvoiceService invoiceService, IExportService exportService, IEmailService emailService)
    {
        _invoiceService = invoiceService;
        _exportService = exportService;
        _emailService = emailService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<InvoiceSummaryDto>>> GetAll([FromQuery] InvoiceQuery query, CancellationToken ct)
    {
        return Ok(await _invoiceService.GetPagedAsync(query, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InvoiceDto>> GetById(Guid id, CancellationToken ct)
    {
        return Ok(await _invoiceService.GetByIdAsync(id, ct));
    }

    [HttpGet("next-number")]
    public async Task<ActionResult<string>> GetNextNumber([FromQuery] DateTime? date, CancellationToken ct)
    {
        return Ok(new { number = await _invoiceService.GetNextNumberAsync(date, ct) });
    }

    [HttpPost]
    public async Task<ActionResult<InvoiceDto>> Create([FromBody] CreateInvoiceRequest request, CancellationToken ct)
    {
        var invoice = await _invoiceService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = invoice.Id }, invoice);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<InvoiceDto>> Update(Guid id, [FromBody] UpdateInvoiceRequest request, CancellationToken ct)
    {
        request.Id = id;
        return Ok(await _invoiceService.UpdateAsync(id, request, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _invoiceService.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/finalize")]
    public async Task<ActionResult<InvoiceDto>> Finalize(Guid id, CancellationToken ct)
    {
        return Ok(await _invoiceService.FinalizeAsync(id, ct));
    }

    [HttpPost("{id:guid}/pay")]
    public async Task<ActionResult<InvoiceDto>> MarkPaid(Guid id, [FromBody] MarkPaidRequest request, CancellationToken ct)
    {
        return Ok(await _invoiceService.MarkPaidAsync(id, request, ct));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<InvoiceDto>> Cancel(Guid id, [FromQuery] string? reason, CancellationToken ct)
    {
        return Ok(await _invoiceService.CancelAsync(id, reason, ct));
    }

    [HttpPost("{id:guid}/duplicate")]
    public async Task<ActionResult<InvoiceDto>> Duplicate(Guid id, CancellationToken ct)
    {
        return Ok(await _invoiceService.DuplicateAsync(id, ct));
    }

    [HttpPost("{id:guid}/credit-note")]
    public async Task<ActionResult<InvoiceDto>> CreateCreditNote(Guid id, CancellationToken ct)
    {
        return Ok(await _invoiceService.CreateCreditNoteAsync(id, ct));
    }

    [HttpPost("{id:guid}/payments")]
    public async Task<IActionResult> AddPayment(Guid id, [FromBody] PaymentRequest request, CancellationToken ct)
    {
        await _invoiceService.RegisterPaymentAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/payments/{paymentId:guid}")]
    public async Task<IActionResult> DeletePayment(Guid id, Guid paymentId, CancellationToken ct)
    {
        await _invoiceService.DeletePaymentAsync(id, paymentId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/import-lines")]
    public async Task<ActionResult<int>> ImportLines(Guid id, [FromBody] IEnumerable<ImportLineRequest> lines, CancellationToken ct)
    {
        return Ok(new { added = await _invoiceService.ImportLinesAsync(id, lines, ct) });
    }

    // ------- Exports -------

    [HttpGet("{id:guid}/export/pdf")]
    public async Task<IActionResult> ExportPdf(
        Guid id,
        [FromQuery] string? lang,
        [FromQuery] string? docFontFamily,
        [FromQuery] string? docBaseFontSize,
        [FromQuery] string? docTableFontSize,
        [FromQuery] string? docHeaderFontSize,
        [FromQuery] string? docFooterFontSize,
        CancellationToken ct)
    {
        var typography = TypographyOptions.FromQuery(docFontFamily, docBaseFontSize, docTableFontSize, docHeaderFontSize, docFooterFontSize);
        var bytes = await _exportService.ExportPdfAsync(id, lang, typography, ct);
        return File(bytes, "application/pdf", $"{await GetFileNameAsync(id, ct)}.pdf");
    }

    [HttpGet("{id:guid}/export/xlsx")]
    public async Task<IActionResult> ExportExcel(
        Guid id,
        [FromQuery] string? lang,
        [FromQuery] string? docFontFamily,
        [FromQuery] string? docBaseFontSize,
        [FromQuery] string? docTableFontSize,
        [FromQuery] string? docHeaderFontSize,
        [FromQuery] string? docFooterFontSize,
        CancellationToken ct)
    {
        var typography = TypographyOptions.FromQuery(docFontFamily, docBaseFontSize, docTableFontSize, docHeaderFontSize, docFooterFontSize);
        var bytes = await _exportService.ExportExcelAsync(id, lang, typography, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{await GetFileNameAsync(id, ct)}.xlsx");
    }

    [HttpGet("{id:guid}/export/docx")]
    public async Task<IActionResult> ExportWord(
        Guid id,
        [FromQuery] string? lang,
        [FromQuery] string? docFontFamily,
        [FromQuery] string? docBaseFontSize,
        [FromQuery] string? docTableFontSize,
        [FromQuery] string? docHeaderFontSize,
        [FromQuery] string? docFooterFontSize,
        CancellationToken ct)
    {
        var typography = TypographyOptions.FromQuery(docFontFamily, docBaseFontSize, docTableFontSize, docHeaderFontSize, docFooterFontSize);
        var bytes = await _exportService.ExportWordAsync(id, lang, typography, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"{await GetFileNameAsync(id, ct)}.docx");
    }

    [HttpGet("{id:guid}/export/csv")]
    public async Task<IActionResult> ExportCsv(Guid id, [FromQuery] string? lang, CancellationToken ct)
    {
        var invoice = await _invoiceService.GetByIdAsync(id, ct);
        var bytes = _exportService.ExportCsv(invoice, lang);
        return File(bytes, "text/csv", $"{await GetFileNameAsync(id, ct)}.csv");
    }

    [HttpGet("export/xlsx")]
    public async Task<IActionResult> ExportInvoicesExcel([FromQuery] InvoiceQuery query, [FromQuery] string? lang, CancellationToken ct)
    {
        var result = await _invoiceService.GetPagedAsync(query, ct);
        var bytes = await _exportService.ExportInvoicesExcelAsync(result.Items, lang, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"factures-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

     [HttpPost("{id:guid}/send-email")]
    public async Task<IActionResult> SendEmail(Guid id, [FromQuery] string to, [FromQuery] string? message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(to))
        {
            return BadRequest(new { message = "L'adresse e-mail du destinataire est obligatoire." });
        }

        if (!IsValidEmail(to))
        {
            return BadRequest(new { message = "L'adresse e-mail du destinataire est invalide." });
        }

        await _emailService.SendInvoiceAsync(id, to, message, ct);
        return Ok(new { message = "Facture envoyée par e-mail." });
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return !string.IsNullOrWhiteSpace(addr.Address) && email.Contains('@');
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> GetFileNameAsync(Guid id, CancellationToken ct)
    {
        var invoice = await _invoiceService.GetByIdAsync(id, ct);
        return invoice.InvoiceNumber;
    }
}
