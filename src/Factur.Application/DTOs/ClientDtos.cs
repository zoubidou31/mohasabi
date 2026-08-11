using Factur.Domain.Enums;

namespace Factur.Application.DTOs;

public class ClientDto
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? Sector { get; set; }
    public string? NIF { get; set; }
    public string? RC { get; set; }
    public string? ART { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Wilaya { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public ClientType Type { get; set; }
    public PaymentMethod? DefaultPaymentMethod { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; }
    public bool IsActive { get; set; } = true;
    public int InvoiceCount { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal Outstanding { get; set; }
    public DateTime? LastInvoiceDate { get; set; }
}

public class CreateClientRequest
{
    public string DisplayName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? Sector { get; set; }
    public string? NIF { get; set; }
    public string? RC { get; set; }
    public string? ART { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Wilaya { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public ClientType Type { get; set; } = ClientType.Particulier;
    public PaymentMethod? DefaultPaymentMethod { get; set; }
    public string? Notes { get; set; }
}

public class UpdateClientRequest : CreateClientRequest { }

public class ClientQuery
{
    public string? Search { get; set; }
    public ClientType? Type { get; set; }
    public string? Status { get; set; } // "active" (défaut) | "archived" | "all"
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class ClientStatsDto
{
    public Guid ClientId { get; set; }
    public int InvoiceCount { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal Outstanding { get; set; }
    public DateTime? LastInvoiceDate { get; set; }
    public IReadOnlyList<InvoiceSummaryDto> RecentInvoices { get; set; } = new List<InvoiceSummaryDto>();
}
