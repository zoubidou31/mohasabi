using Factur.Application.Common.Exceptions;
using Factur.Application.Common.Mapping;
using Factur.Application.DTOs;
using Factur.Application.Interfaces;
using Factur.Domain.Entities;
using Factur.Domain.Enums;
using Factur.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Factur.Infrastructure.Services;

public class ClientService : IClientService
{
    private readonly ApplicationDbContext _context;
    private readonly IValidator<CreateClientRequest> _validator;
    private readonly IAuditLogger _auditLogger;

    public ClientService(ApplicationDbContext context, IValidator<CreateClientRequest> validator, IAuditLogger auditLogger)
    {
        _context = context;
        _validator = validator;
        _auditLogger = auditLogger;
    }

    public async Task<Guid> CreateAsync(CreateClientRequest request, CancellationToken ct = default)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        var client = new Client();
        Apply(request, client);
        _context.Clients.Add(client);
        await _context.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Client", client.Id.ToString(), "Création", new { client.DisplayName }, ct);
        return client.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateClientRequest request, CancellationToken ct = default)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        var client = await _context.Clients.FindAsync([id], ct)
            ?? throw new NotFoundException("Client introuvable.");
        Apply(request, client);
        client.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Client", id.ToString(), "Modification", new { client.DisplayName }, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var client = await _context.Clients.FindAsync([id], ct)
            ?? throw new NotFoundException("Client introuvable.");

        // On ne supprime jamais les documents comptables (factures, lignes, paiements...).
        // Si le client possède des factures ou tout autre document associé, on bloque la
        // suppression définitive (aucun cascade-delete n'est appliqué).
        var hasDependencies = await _context.Invoices.AnyAsync(i => i.ClientId == id, ct);
        if (hasDependencies)
        {
            throw new ConflictException(
                "Impossible de supprimer ce client car il possède des factures ou documents associés.");
        }

        _context.Clients.Remove(client);
        await _context.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Client", id.ToString(), "Suppression", new { client.DisplayName }, ct);
    }

    public async Task ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var client = await _context.Clients.FindAsync([id], ct)
            ?? throw new NotFoundException("Client introuvable.");

        // L'archivage ne modifie jamais les factures/paiements : on ne change que le statut du client.
        client.IsActive = false;
        client.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Client", id.ToString(), "Archivage", new { client.DisplayName }, ct);
    }

    public async Task<ClientDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var client = await _context.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Client introuvable.");

        var stats = await ComputeStatsAsync(id, ct);
        return client.ToDto(stats.InvoiceCount, stats.TotalSpent, stats.Outstanding, stats.LastInvoiceDate);
    }

    public async Task<PagedResult<ClientDto>> GetPagedAsync(ClientQuery query, CancellationToken ct = default)
    {
        var q = _context.Clients.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            q = q.Where(c => c.DisplayName.ToLower().Contains(search)
                             || (c.CompanyName != null && c.CompanyName.ToLower().Contains(search))
                             || c.Phone.Contains(search));
        }

        if (query.Type.HasValue)
        {
            q = q.Where(c => c.Type == query.Type);
        }

        // Filtrage par statut (actifs par défaut). Un client archivé reste lié à ses factures.
        var status = (query.Status ?? "active")?.Trim().ToLowerInvariant() ?? "active";
        if (status == "active")
        {
            q = q.Where(c => c.IsActive);
        }
        else if (status == "archived")
        {
            q = q.Where(c => !c.IsActive);
        }

        var totalCount = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Min(100, Math.Max(1, query.PageSize));

        var clients = await q
            .OrderBy(c => c.DisplayName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var result = new List<ClientDto>();
        foreach (var client in clients)
        {
            var stats = await ComputeStatsAsync(client.Id, ct);
            result.Add(client.ToDto(stats.InvoiceCount, stats.TotalSpent, stats.Outstanding, stats.LastInvoiceDate));
        }

        return new PagedResult<ClientDto>
        {
            Items = result,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<ClientStatsDto> GetStatsAsync(Guid id, CancellationToken ct = default)
    {
        if (!await _context.Clients.AsNoTracking().AnyAsync(c => c.Id == id, ct))
        {
            throw new NotFoundException("Client introuvable.");
        }

        var (invoiceCount, totalSpent, outstanding, lastInvoiceDate) = await ComputeStatsAsync(id, ct);

        var invoices = await _context.Invoices.AsNoTracking()
            .Where(i => i.ClientId == id)
            .OrderByDescending(i => i.InvoiceDate)
            .Take(10)
            .Include(i => i.Client)
            .ToListAsync(ct);

        var totalPaid = await _context.Payments.AsNoTracking()
            .Where(p => p.Invoice!.ClientId == id)
            .SumAsync(p => p.Amount, ct);

        return new ClientStatsDto
        {
            ClientId = id,
            InvoiceCount = invoiceCount,
            TotalSpent = totalSpent,
            TotalPaid = totalPaid,
            Outstanding = Math.Max(0m, outstanding),
            LastInvoiceDate = lastInvoiceDate,
            RecentInvoices = invoices.Select(i => i.ToSummaryDto()).ToList(),
        };
    }

    public async Task<int> ImportAsync(IEnumerable<CreateClientRequest> clients, CancellationToken ct = default)
    {
        var list = clients.ToList();
        if (list.Count == 0)
        {
            return 0;
        }

        var imported = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var request in list)
        {
            var validation = await _validator.ValidateAsync(request, ct);
            if (!validation.IsValid || string.IsNullOrWhiteSpace(request.DisplayName))
            {
                continue;
            }

            var displayName = request.DisplayName.Trim();
            if (!seen.Add(displayName))
            {
                continue;
            }

            var exists = await _context.Clients.AnyAsync(c => c.DisplayName == displayName, ct);
            if (exists)
            {
                continue;
            }

            var client = new Client();
            Apply(request, client);
            _context.Clients.Add(client);
            imported++;
        }

        await _context.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Client", "import", "Import Excel", new { imported }, ct);
        return imported;
    }

    private async Task<(int InvoiceCount, decimal TotalSpent, decimal Outstanding, DateTime? LastInvoiceDate)> ComputeStatsAsync(Guid clientId, CancellationToken ct)
    {
        var invoices = await _context.Invoices.AsNoTracking()
            .Where(i => i.ClientId == clientId)
            .Select(i => new { i.InvoiceDate, i.TotalTTC, i.SoldeRestant, i.Status })
            .ToListAsync(ct);

        var count = invoices.Count;
        var spent = invoices
            .Where(i => i.Status != InvoiceStatus.Annulee)
            .Sum(i => i.TotalTTC);
        var outstanding = invoices
            .Where(i => i.Status != InvoiceStatus.Annulee && i.Status != InvoiceStatus.Brouillon)
            .Sum(i => i.SoldeRestant);
        var last = invoices.OrderByDescending(i => i.InvoiceDate).FirstOrDefault()?.InvoiceDate;

        return (count, spent, outstanding, last);
    }

    private static void Apply(CreateClientRequest request, Client client)
    {
        client.DisplayName = request.DisplayName.Trim();
        client.CompanyName = request.CompanyName?.Trim();
        client.Sector = request.Sector?.Trim();
        client.NIF = request.NIF?.Trim();
        client.RC = request.RC?.Trim();
        client.ART = request.ART?.Trim();
        client.Address = request.Address?.Trim() ?? string.Empty;
        client.PostalCode = request.PostalCode;
        client.City = request.City;
        client.Wilaya = request.Wilaya;
        client.Phone = request.Phone?.Trim() ?? string.Empty;
        client.Mobile = request.Mobile;
        client.Email = request.Email?.Trim();
        client.Type = request.Type;
        client.DefaultPaymentMethod = request.DefaultPaymentMethod;
        client.Notes = request.Notes;
    }
}
