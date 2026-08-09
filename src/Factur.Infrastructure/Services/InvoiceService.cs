using Factur.Application.Common.Exceptions;
using Factur.Application.Common.Interfaces;
using Factur.Application.Common.Mapping;
using Factur.Application.DTOs;
using Factur.Application.Interfaces;
using Factur.Domain.Entities;
using Factur.Domain.Enums;
using Factur.Domain.Services;
using Factur.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Factur.Infrastructure.Services;

public class InvoiceService : IInvoiceService
{
    private readonly ApplicationDbContext _context;
    private readonly IValidator<CreateInvoiceRequest> _validator;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUserService _currentUser;

    public InvoiceService(ApplicationDbContext context, IValidator<CreateInvoiceRequest> validator, IAuditLogger auditLogger, ICurrentUserService currentUser)
    {
        _context = context;
        _validator = validator;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
    }

    public async Task<InvoiceDto> CreateAsync(CreateInvoiceRequest request, CancellationToken ct = default)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        var company = await _context.Companies.OrderBy(c => c.CreatedDate).FirstOrDefaultAsync(ct)
            ?? throw new BusinessRuleException("Veuillez d'abord configurer votre société (Paramètres).");

        if (!await _context.Clients.AnyAsync(c => c.Id == request.ClientId, ct))
        {
            throw new NotFoundException("Client introuvable.");
        }

        var invoice = new Invoice
        {
            ClientId = request.ClientId,
            CompanyId = company.Id,
            InvoiceDate = request.InvoiceDate.Date,
            ValidityDays = request.ValidityDays,
            DueDate = request.ValidityDays > 0 ? request.InvoiceDate.Date.AddDays(request.ValidityDays) : null,
            InvoiceType = request.InvoiceType,
            PaymentMethod = request.PaymentMethod,
            ChequeNumber = request.ChequeNumber,
            OrderReference = request.OrderReference,
            BonCommande = request.BonCommande,
            Notes = request.Notes,
            MentionsSpecifiques = request.MentionsSpecifiques,
            PaymentConditions = request.PaymentConditions ?? company.PaymentConditions,
            Penalties = request.Penalties ?? company.Penalties,
            RemiseValue = request.RemiseValue,
            RemiseIsPercentage = request.RemiseIsPercentage,
            FraisPort = request.FraisPort,
            FraisPortLabel = request.FraisPortLabel,
            AutresFrais = request.AutresFrais,
            AutresFraisLabel = request.AutresFraisLabel,
            CreatedBy = CurrentUserId,
        };

        await AssignNextNumberAsync(invoice, ct);
        ApplyLines(invoice, request.Lines);
        FactureCalculator.RecalculateInvoice(invoice, company.UseBankersRounding);

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Invoice", invoice.Id.ToString(), "Création", new { invoice.InvoiceNumber, Total = invoice.TotalTTC }, ct);

        return (await LoadAsync(invoice.Id, ct)).ToFullDto();
    }

    public async Task<InvoiceDto> UpdateAsync(Guid id, UpdateInvoiceRequest request, CancellationToken ct = default)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        var invoice = await GetForEditAsync(id, ct);
        var company = await _context.Companies.OrderBy(c => c.CreatedDate).FirstAsync(ct);

        if (!await _context.Clients.AnyAsync(c => c.Id == request.ClientId, ct))
        {
            throw new NotFoundException("Client introuvable.");
        }

        invoice.ClientId = request.ClientId;
        invoice.InvoiceDate = request.InvoiceDate.Date;
        invoice.ValidityDays = request.ValidityDays;
        invoice.DueDate = request.ValidityDays > 0 ? request.InvoiceDate.Date.AddDays(request.ValidityDays) : null;
        invoice.InvoiceType = request.InvoiceType;
        invoice.PaymentMethod = request.PaymentMethod;
        invoice.ChequeNumber = request.ChequeNumber;
        invoice.OrderReference = request.OrderReference;
        invoice.BonCommande = request.BonCommande;
        invoice.Notes = request.Notes;
        invoice.MentionsSpecifiques = request.MentionsSpecifiques;
        invoice.PaymentConditions = request.PaymentConditions ?? company.PaymentConditions;
        invoice.Penalties = request.Penalties ?? company.Penalties;
        invoice.RemiseValue = request.RemiseValue;
        invoice.RemiseIsPercentage = request.RemiseIsPercentage;
        invoice.FraisPort = request.FraisPort;
        invoice.FraisPortLabel = request.FraisPortLabel;
        invoice.AutresFrais = request.AutresFrais;
        invoice.AutresFraisLabel = request.AutresFraisLabel;

        _context.InvoiceLines.RemoveRange(invoice.Lines);
        invoice.Lines.Clear();

        var oldBreakdowns = await _context.TVABreakdowns.Where(b => b.InvoiceId == id).ToListAsync(ct);
        _context.TVABreakdowns.RemoveRange(oldBreakdowns);

        ApplyLines(invoice, request.Lines);
        FactureCalculator.RecalculateInvoice(invoice, company.UseBankersRounding);
        invoice.UpdatedDate = DateTime.UtcNow;

        MarkNewChildrenAdded(invoice);

        await _context.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Invoice", id.ToString(), "Modification", new { invoice.InvoiceNumber, Total = invoice.TotalTTC }, ct);

        return (await LoadAsync(id, ct)).ToFullDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var invoice = await GetForEditAsync(id, ct);
        _context.Invoices.Remove(invoice);
        await _context.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Invoice", id.ToString(), "Suppression", new { invoice.InvoiceNumber }, ct);
    }

    public async Task<InvoiceDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var invoice = await LoadAsync(id, ct);
        return invoice.ToFullDto();
    }

    public async Task<PagedResult<InvoiceSummaryDto>> GetPagedAsync(InvoiceQuery query, CancellationToken ct = default)
    {
        var q = _context.Invoices.AsNoTracking().Include(i => i.Client).AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(i => i.InvoiceNumber.ToLower().Contains(s) || i.Client!.DisplayName.ToLower().Contains(s));
        }

        if (query.ClientId.HasValue) q = q.Where(i => i.ClientId == query.ClientId);
        if (query.Status.HasValue) q = q.Where(i => i.Status == query.Status);
        if (query.InvoiceType.HasValue) q = q.Where(i => i.InvoiceType == query.InvoiceType);
        if (query.From.HasValue) q = q.Where(i => i.InvoiceDate >= query.From.Value.Date);
        if (query.To.HasValue) q = q.Where(i => i.InvoiceDate <= query.To.Value.Date);
        if (query.MinAmount.HasValue) q = q.Where(i => (double)i.TotalTTC >= (double)query.MinAmount.Value);
        if (query.MaxAmount.HasValue) q = q.Where(i => (double)i.TotalTTC <= (double)query.MaxAmount.Value);
        if (query.Overdue == true) q = q.Where(i => i.Status != InvoiceStatus.Payee && i.Status != InvoiceStatus.Annulee && i.DueDate.HasValue && i.DueDate.Value.Date < DateTime.UtcNow.Date);

        var totalCount = await q.CountAsync(ct);

        q = query.SortBy?.ToLower() switch
        {
            "number" => query.SortDescending ? q.OrderByDescending(i => i.InvoiceNumber) : q.OrderBy(i => i.InvoiceNumber),
            "date" => query.SortDescending ? q.OrderByDescending(i => i.InvoiceDate) : q.OrderBy(i => i.InvoiceDate),
            "client" => query.SortDescending ? q.OrderByDescending(i => i.Client!.DisplayName) : q.OrderBy(i => i.Client!.DisplayName),
            "total" => query.SortDescending ? q.OrderByDescending(i => (double)i.TotalTTC) : q.OrderBy(i => (double)i.TotalTTC),
            "status" => query.SortDescending ? q.OrderByDescending(i => i.Status) : q.OrderBy(i => i.Status),
            _ => q.OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Sequence),
        };

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<InvoiceSummaryDto>
        {
            Items = items.Select(i => i.ToSummaryDto()).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<InvoiceDto> FinalizeAsync(Guid id, CancellationToken ct = default)
    {
        var invoice = await GetForEditAsync(id, ct);
        if (invoice.Status != InvoiceStatus.Brouillon)
        {
            throw new BusinessRuleException("Seule une facture en brouillon peut être finalisée.");
        }

        invoice.Status = InvoiceStatus.Finalisee;
        invoice.FinalizedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Invoice", id.ToString(), "Finalisation", new { invoice.InvoiceNumber }, ct);

        return (await LoadAsync(id, ct)).ToFullDto();
    }

    public async Task<InvoiceDto> MarkPaidAsync(Guid id, MarkPaidRequest request, CancellationToken ct = default)
    {
        var invoice = await LoadForPaymentAsync(id, ct);
        if (invoice.Status is InvoiceStatus.Annulee or InvoiceStatus.Payee)
        {
            throw new BusinessRuleException("Une facture annulée ou déjà réglée ne peut pas être réglée.");
        }

        var paymentDate = request.PaymentDate ?? DateTime.UtcNow;
        var amount = request.Amount ?? invoice.SoldeRestant;

        if (amount <= 0)
        {
            throw new BusinessRuleException("Le montant du paiement doit être positif.");
        }

        _context.Payments.Add(new Payment
        {
            InvoiceId = invoice.Id,
            PaymentDate = paymentDate,
            Amount = Math.Min(amount, invoice.SoldeRestant),
            PaymentMethod = request.PaymentMethod,
            ChequeNumber = request.ChequeNumber,
            Notes = request.Notes,
        });

        await _context.SaveChangesAsync(ct);
        await RefreshPaidStateAsync(invoice, ct);
        await _context.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Invoice", id.ToString(), "Paiement", new { amount, status = invoice.Status }, ct);

        return (await LoadAsync(id, ct)).ToFullDto();
    }

    public async Task<InvoiceDto> CancelAsync(Guid id, string? reason, CancellationToken ct = default)
    {
        var invoice = await GetForEditAsync(id, ct);
        if (invoice.Status == InvoiceStatus.Annulee)
        {
            throw new BusinessRuleException("Cette facture est déjà annulée.");
        }

        invoice.Status = InvoiceStatus.Annulee;
        invoice.CancelledDate = DateTime.UtcNow;
        invoice.Notes = reason is null ? invoice.Notes : (invoice.Notes is null ? reason : $"{invoice.Notes}\n[Annulée] {reason}");
        await _context.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Invoice", id.ToString(), "Annulation", new { reason }, ct);

        return (await LoadAsync(id, ct)).ToFullDto();
    }

    public async Task<InvoiceDto> DuplicateAsync(Guid id, CancellationToken ct = default)
    {
        var source = await LoadAsync(id, ct);
        var company = await _context.Companies.OrderBy(c => c.CreatedDate).FirstAsync(ct);

        var copy = new Invoice
        {
            ClientId = source.ClientId,
            CompanyId = source.CompanyId,
            InvoiceDate = DateTime.UtcNow.Date,
            ValidityDays = source.ValidityDays,
            DueDate = source.DueDate,
            InvoiceType = source.InvoiceType,
            Status = InvoiceStatus.Brouillon,
            PaymentMethod = source.PaymentMethod,
            OrderReference = source.OrderReference,
            BonCommande = source.BonCommande,
            Notes = source.Notes,
            MentionsSpecifiques = source.MentionsSpecifiques,
            PaymentConditions = source.PaymentConditions,
            Penalties = source.Penalties,
            RemiseValue = source.RemiseValue,
            RemiseIsPercentage = source.RemiseIsPercentage,
            FraisPort = source.FraisPort,
            FraisPortLabel = source.FraisPortLabel,
            AutresFrais = source.AutresFrais,
            AutresFraisLabel = source.AutresFraisLabel,
            CreatedBy = CurrentUserId,
        };

        await AssignNextNumberAsync(copy, ct);
        foreach (var line in source.Lines)
        {
            copy.Lines.Add(new InvoiceLine
            {
                ProductId = line.ProductId,
                Reference = line.Reference,
                Description = line.Description,
                Quantity = line.Quantity,
                UnitPriceHT = line.UnitPriceHT,
                TVARate = line.TVARate,
                SortOrder = line.SortOrder,
            });
        }

        FactureCalculator.RecalculateInvoice(copy, company.UseBankersRounding);
        _context.Invoices.Add(copy);
        await _context.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Invoice", copy.Id.ToString(), "Duplication", new { from = source.InvoiceNumber }, ct);

        return (await LoadAsync(copy.Id, ct)).ToFullDto();
    }

    public async Task<InvoiceDto> CreateCreditNoteAsync(Guid id, CancellationToken ct = default)
    {
        var source = await LoadAsync(id, ct);
        var company = await _context.Companies.OrderBy(c => c.CreatedDate).FirstAsync(ct);

        var avoir = new Invoice
        {
            ClientId = source.ClientId,
            CompanyId = source.CompanyId,
            InvoiceDate = DateTime.UtcNow.Date,
            ValidityDays = 0,
            DueDate = null,
            InvoiceType = InvoiceType.Avoir,
            Status = InvoiceStatus.Brouillon,
            PaymentMethod = source.PaymentMethod,
            Notes = $"Avoir émis pour la facture {source.InvoiceNumber}.",
            CreatedBy = CurrentUserId,
            CreditNoteForInvoiceId = source.Id,
        };

        await AssignNextNumberAsync(avoir, ct);
        foreach (var line in source.Lines)
        {
            avoir.Lines.Add(new InvoiceLine
            {
                ProductId = line.ProductId,
                Reference = line.Reference,
                Description = line.Description,
                Quantity = line.Quantity,
                UnitPriceHT = -line.UnitPriceHT,
                TVARate = line.TVARate,
                SortOrder = line.SortOrder,
            });
        }

        FactureCalculator.RecalculateInvoice(avoir, company.UseBankersRounding);
        _context.Invoices.Add(avoir);
        await _context.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Invoice", avoir.Id.ToString(), "Émission d'avoir", new { forInvoice = source.InvoiceNumber }, ct);

        return (await LoadAsync(avoir.Id, ct)).ToFullDto();
    }

    public async Task<string> GetNextNumberAsync(DateTime? date = null, CancellationToken ct = default)
    {
        var company = await _context.Companies.OrderBy(c => c.CreatedDate).FirstOrDefaultAsync(ct)
            ?? throw new BusinessRuleException("Veuillez d'abord configurer votre société.");

        var invoiceDate = (date ?? DateTime.UtcNow).Date;
        var lastSeq = await _context.Invoices.AsNoTracking()
            .Where(i => i.InvoiceDate.Year == invoiceDate.Year && i.InvoiceDate.Month == invoiceDate.Month)
            .Select(i => (int?)i.Sequence)
            .MaxAsync(ct) ?? 0;

        return FormatNumber(company.InvoicePrefix, invoiceDate, lastSeq + 1);
    }

    public async Task RegisterPaymentAsync(Guid invoiceId, PaymentRequest request, CancellationToken ct = default)
    {
        var invoice = await LoadForPaymentAsync(invoiceId, ct);
        if (invoice.Status is InvoiceStatus.Annulee or InvoiceStatus.Payee)
        {
            throw new BusinessRuleException("Impossible d'ajouter un paiement sur une facture annulée ou déjà réglée.");
        }

        if (request.Amount <= 0)
        {
            throw new BusinessRuleException("Le montant du paiement doit être positif.");
        }

        _context.Payments.Add(new Payment
        {
            InvoiceId = invoice.Id,
            PaymentDate = request.PaymentDate,
            Amount = Math.Min(request.Amount, invoice.SoldeRestant),
            PaymentMethod = request.PaymentMethod,
            ChequeNumber = request.ChequeNumber,
            Notes = request.Notes,
        });

        await _context.SaveChangesAsync(ct);
        await RefreshPaidStateAsync(invoice, ct);
        await _context.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Payment", invoiceId.ToString(), "Paiement partiel", new { request.Amount }, ct);
    }

    public async Task DeletePaymentAsync(Guid invoiceId, Guid paymentId, CancellationToken ct = default)
    {
        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.Id == paymentId && p.InvoiceId == invoiceId, ct)
            ?? throw new NotFoundException("Paiement introuvable.");

        _context.Payments.Remove(payment);
        await _context.SaveChangesAsync(ct);

        var invoice = await _context.Invoices.FindAsync([invoiceId], ct);
        if (invoice is not null)
        {
            await RefreshPaidStateAsync(invoice, ct);
            await _context.SaveChangesAsync(ct);
        }

        await _auditLogger.LogAsync("Payment", invoiceId.ToString(), "Suppression de paiement", new { paymentId }, ct);
    }

    public async Task<int> ImportLinesAsync(Guid invoiceId, IEnumerable<ImportLineRequest> lines, CancellationToken ct = default)
    {
        var invoice = await GetForEditAsync(invoiceId, ct);
        var company = await _context.Companies.OrderBy(c => c.CreatedDate).FirstAsync(ct);
        var products = await _context.Products.AsNoTracking().ToListAsync(ct);

        var list = lines.ToList();
        if (list.Count == 0)
        {
            return 0;
        }

        var added = 0;
        var createdLines = new List<InvoiceLine>();
        var maxOrder = invoice.Lines.Count == 0 ? 0 : invoice.Lines.Max(l => l.SortOrder);
        foreach (var line in list)
        {
            if (line.Quantity <= 0 || line.UnitPriceHT < 0)
            {
                continue;
            }

            var product = products.FirstOrDefault(p => p.Reference.Equals(line.Reference.Trim(), StringComparison.OrdinalIgnoreCase));
            var newLine = new InvoiceLine
            {
                ProductId = product?.Id,
                Reference = product?.Reference ?? line.Reference.Trim(),
                Description = product?.Name ?? line.Reference.Trim(),
                Quantity = line.Quantity,
                UnitPriceHT = line.UnitPriceHT,
                TVARate = line.TVARate ?? product?.DefaultTVARate ?? company.DefaultTVARate,
                SortOrder = ++maxOrder,
            };
            invoice.Lines.Add(newLine);
            createdLines.Add(newLine);
            added++;
        }

        var oldBreakdowns = await _context.TVABreakdowns.Where(b => b.InvoiceId == invoiceId).ToListAsync(ct);
        _context.TVABreakdowns.RemoveRange(oldBreakdowns);

        FactureCalculator.RecalculateInvoice(invoice, company.UseBankersRounding);
        foreach (var newLine in createdLines)
        {
            _context.Entry(newLine).State = EntityState.Added;
        }

        foreach (var breakdown in invoice.TVABreakdowns)
        {
            _context.Entry(breakdown).State = EntityState.Added;
        }

        await _context.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Invoice", invoiceId.ToString(), "Import de lignes Excel", new { added }, ct);
        return added;
    }

    // ---------------------------------------------------------------- privé

    private Guid? CurrentUserId => _currentUser.UserId;

    private static string FormatNumber(string prefix, DateTime date, int sequence) =>
        $"{prefix}-{date:yyyy-MM}-{sequence:000000}";

    private async Task AssignNextNumberAsync(Invoice invoice, CancellationToken ct)
    {
        var company = await _context.Companies.OrderBy(c => c.CreatedDate).FirstAsync(ct);
        var lastSeq = await _context.Invoices.AsNoTracking()
            .Where(i => i.InvoiceDate.Year == invoice.InvoiceDate.Year && i.InvoiceDate.Month == invoice.InvoiceDate.Month)
            .Select(i => (int?)i.Sequence)
            .MaxAsync(ct) ?? 0;

        invoice.Sequence = lastSeq + 1;
        invoice.InvoiceNumber = FormatNumber(company.InvoicePrefix, invoice.InvoiceDate, invoice.Sequence);
    }

    private async Task<Invoice> LoadAsync(Guid id, CancellationToken ct)
    {
        return await _context.Invoices.AsNoTracking()
            .Include(i => i.Client)
            .Include(i => i.Lines)
            .Include(i => i.TVABreakdowns)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException("Facture introuvable.");
    }

    private async Task<Invoice> GetForEditAsync(Guid id, CancellationToken ct)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException("Facture introuvable.");

        if (invoice.Status != InvoiceStatus.Brouillon)
        {
            throw new BusinessRuleException("Cette facture n'est plus modifiable (statut : " + invoice.Status + ").");
        }

        return invoice;
    }

    private async Task<Invoice> LoadForPaymentAsync(Guid id, CancellationToken ct)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException("Facture introuvable.");

        if (invoice.Status is InvoiceStatus.Annulee)
        {
            throw new BusinessRuleException("Une facture annulée ne peut pas recevoir de paiement.");
        }

        return invoice;
    }

    private static void ApplyLines(Invoice invoice, IEnumerable<InvoiceLineRequest> lines)
    {
        var order = 0;
        foreach (var line in lines)
        {
            invoice.Lines.Add(new InvoiceLine
            {
                ProductId = line.ProductId,
                Reference = line.Reference?.Trim() ?? string.Empty,
                Description = line.Description?.Trim() ?? string.Empty,
                Quantity = line.Quantity,
                UnitPriceHT = line.UnitPriceHT,
                TVARate = line.TVARate,
                SortOrder = order++,
            });
        }
    }

    /// <summary>
    /// Les entités ajoutées à une collection d'un parent déjà suivi (parent en base)
    /// sont découvertes par EF Core comme Modified (clé Guid non temporaire).
    /// Elles doivent être explicitement marquées Added pour être insérées.
    /// </summary>
    private void MarkNewChildrenAdded(Invoice invoice)
    {
        foreach (var line in invoice.Lines)
        {
            _context.Entry(line).State = EntityState.Added;
        }

        foreach (var breakdown in invoice.TVABreakdowns)
        {
            _context.Entry(breakdown).State = EntityState.Added;
        }
    }

    private async Task RefreshPaidStateAsync(Invoice invoice, CancellationToken ct)
    {
        var paid = await _context.Payments.Where(p => p.InvoiceId == invoice.Id).SumAsync(p => p.Amount, ct);
        invoice.MontantPaye = paid;

        if (invoice.TotalTTC > 0m && paid >= invoice.TotalTTC)
        {
            invoice.Status = InvoiceStatus.Payee;
            invoice.PaidDate = DateTime.UtcNow;
        }
    }
}
