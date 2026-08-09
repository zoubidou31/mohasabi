using Factur.Domain.Enums;

namespace Factur.Application.DTOs;

public class CompanyDto
{
    public Guid Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? LogoPath { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Wilaya { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? Mobile { get; set; }
    public string Email { get; set; } = string.Empty;
    public string NIF { get; set; } = string.Empty;
    public string NIS { get; set; } = string.Empty;
    public string RC { get; set; } = string.Empty;
    public string ART { get; set; } = string.Empty;
    public string? RIB { get; set; }
    public string? CCP { get; set; }
    public string? BankName { get; set; }
    public string InvoicePrefix { get; set; } = "FAC";
    public string InvoiceSerie { get; set; } = string.Empty;
    public int ValidityDays { get; set; } = 30;
    public TVARate DefaultTVARate { get; set; } = TVARate.Normal;
    public string? PaymentConditions { get; set; }
    public string? Penalties { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? StampPath { get; set; }
    public bool UseBankersRounding { get; set; }
}

public class UpdateCompanyRequest
{
    public string CompanyName { get; set; } = string.Empty;
    public string? LogoData { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Wilaya { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? Mobile { get; set; }
    public string Email { get; set; } = string.Empty;
    public string NIF { get; set; } = string.Empty;
    public string NIS { get; set; } = string.Empty;
    public string RC { get; set; } = string.Empty;
    public string ART { get; set; } = string.Empty;
    public string? RIB { get; set; }
    public string? CCP { get; set; }
    public string? BankName { get; set; }
    public string InvoicePrefix { get; set; } = "FAC";
    public string InvoiceSerie { get; set; } = string.Empty;
    public int ValidityDays { get; set; } = 30;
    public TVARate DefaultTVARate { get; set; } = TVARate.Normal;
    public string? PaymentConditions { get; set; }
    public string? Penalties { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? StampData { get; set; }
    public bool UseBankersRounding { get; set; }
}
