using Factur.Domain.Common;
using Factur.Domain.Enums;

namespace Factur.Domain.Entities;

/// <summary>Client (acheteur).</summary>
public class Client : BaseEntity
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

    /// <summary>Un client archivé est conservé (avec ses factures) mais masqué de la liste active.</summary>
    public bool IsActive { get; set; } = true;
}
