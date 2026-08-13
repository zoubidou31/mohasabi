using Factur.Application.Common.Exceptions;
using Factur.Application.DTOs;
using Factur.Application.Interfaces;
using Factur.Domain.Entities;
using Factur.Infrastructure.Persistence;
using MailKit.Net.Smtp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Factur.Infrastructure.Services;

/// <summary>Paramètres SMTP pour l'envoi d'e-mails.</summary>
public class EmailSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; }
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
}

public class EmailService : IEmailService
{
    private readonly ApplicationDbContext _context;
    private readonly IExportService _exportService;
    private readonly IOptions<EmailSettings> _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        ApplicationDbContext context,
        IExportService exportService,
        IOptions<EmailSettings> settings,
        ILogger<EmailService> logger)
    {
        _context = context;
        _exportService = exportService;
        _settings = settings;
        _logger = logger;
    }

    public async Task SendInvoiceAsync(Guid invoiceId, string toAddress, string? message, CancellationToken ct = default)
    {
        var invoice = await _context.Invoices.AsNoTracking()
            .Include(i => i.Company)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct)
            ?? throw new KeyNotFoundException("Facture introuvable.");

        var company = invoice.Company ?? new Company();
        var settings = _settings.Value;

        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.FromEmail))
        {
            throw new BadRequestException("Le serveur SMTP n'est pas configuré. Renseignez les paramètres e-mail dans appsettings.json.");
        }

        var email = new MimeMessage
        {
            From = { new MailboxAddress(string.IsNullOrWhiteSpace(settings.FromName) ? company.CompanyName : settings.FromName, settings.FromEmail) },
            To = { new MailboxAddress(string.Empty, toAddress) },
            Subject = $"Facture {invoice.InvoiceNumber} — {company.CompanyName}",
        };

        var body = new TextPart("plain")
        {
            Text = message ?? $"Bonjour,\n\nVeuillez trouver ci-joint la facture {invoice.InvoiceNumber} d'un montant de {invoice.TotalTTC:N2} DA.\n\nCordialement,\n{company.CompanyName}",
        };

        var pdf = await _exportService.ExportPdfAsync(invoiceId, lang: null, typography: null, ct);
        var attachment = new MimePart("application", "pdf")
        {
            FileName = $"{invoice.InvoiceNumber}.pdf",
            Content = new MimeContent(new MemoryStream(pdf)),
        };

        var multipart = new Multipart("mixed") { body, attachment };
        email.Body = multipart;

        using var client = new SmtpClient();
        await client.ConnectAsync(settings.Host, settings.Port, settings.UseSsl ? MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable : MailKit.Security.SecureSocketOptions.Auto, ct);

        if (!string.IsNullOrWhiteSpace(settings.User))
        {
            await client.AuthenticateAsync(settings.User, settings.Password, ct);
        }

        await client.SendAsync(email, ct);
        await client.DisconnectAsync(true, ct);

        _logger.LogInformation("Facture {InvoiceNumber} envoyée à {To}", invoice.InvoiceNumber, toAddress);
    }
}
