using Factur.Domain.Common;
using Factur.Domain.Enums;

namespace Factur.Domain.Entities;

/// <summary>Informations du vendeur (entreprise émettrice). Identifiants fiscaux obligatoires pour la légalité.</summary>
public class Company : BaseEntity
{
    public string CompanyName { get; set; } = string.Empty;
    public string? LogoPath { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Wilaya { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? Mobile { get; set; }
    public string Email { get; set; } = string.Empty;

    // Identifiants fiscaux
    public string NIF { get; set; } = string.Empty;
    public string NIS { get; set; } = string.Empty;
    public string RC { get; set; } = string.Empty;
    public string ART { get; set; } = string.Empty;

    // Coordonnées bancaires
    public string? RIB { get; set; }
    public string? CCP { get; set; }
    public string? BankName { get; set; }

    // Réglages facturation
    public string InvoicePrefix { get; set; } = "FAC";
    public string InvoiceSerie { get; set; } = string.Empty;
    public int ValidityDays { get; set; } = 30;
    public TVARate DefaultTVARate { get; set; } = TVARate.Normal;
    public string? PaymentConditions { get; set; }
    public string? Penalties { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? StampPath { get; set; }

    // Arrondi
    public bool UseBankersRounding { get; set; }
}
