using Factur.Domain.Entities;
using Factur.Domain.Enums;

namespace Factur.Infrastructure.Export;

/// <summary>Modèle unifié d'un document professionnel exporté (facture, avoir, pro-forma).</summary>
public sealed class ExportDocument
{
    public DocumentStrings Strings { get; init; } = null!;
    public InvoiceType InvoiceType { get; init; }
    public string Title { get; init; } = string.Empty;
    public string InvoiceNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string StatusColorHex { get; init; } = "#B45309";
    public DateTime IssueDate { get; init; }
    public DateTime? DueDate { get; init; }
    public string PaymentMethod { get; init; } = string.Empty;
    public string? OrderReference { get; init; }

    public CompanyBlock Company { get; init; } = new();
    public PartyBlock Client { get; init; } = new();

    public IReadOnlyList<ExportLine> Lines { get; init; } = new List<ExportLine>();
    public IReadOnlyList<VatBreakdownBlock> VatBreakdowns { get; init; } = new List<VatBreakdownBlock>();

    public TotalsBlock Totals { get; init; } = new();

    public string? PaymentConditions { get; init; }
    public string? Penalties { get; init; }
    public string? MentionsSpecifiques { get; init; }
    public string? Notes { get; init; }

    /// <summary>Montant total en toutes lettres (dinars algériens).</summary>
    public string AmountInWords { get; init; } = string.Empty;
}

public sealed class CompanyBlock
{
    public string Name { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string NIF { get; init; } = string.Empty;
    public string NIS { get; init; } = string.Empty;
    public string RC { get; init; } = string.Empty;
    public string ART { get; init; } = string.Empty;
    public string? RIB { get; init; }
    public string? CCP { get; init; }
    public string? BankName { get; init; }

    /// <summary>Logo chargé depuis le disque (null si absent).</summary>
    public byte[]? Logo { get; init; }
}

public sealed class PartyBlock
{
    public string Name { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string NIF { get; init; } = string.Empty;
    public string RC { get; init; } = string.Empty;
    public string ART { get; init; } = string.Empty;
}

public sealed class ExportLine
{
    public int Index { get; init; }
    public string Reference { get; init; } = string.Empty;
    public string Designation { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitPriceHT { get; init; }
    public string VatLabel { get; init; } = string.Empty;
    public decimal TotalHT { get; init; }
    public decimal TotalTTC { get; init; }
}

public sealed class VatBreakdownBlock
{
    public string Label { get; init; } = string.Empty;
    public decimal BaseHT { get; init; }
    public decimal VatAmount { get; init; }
    public decimal Ttc { get; init; }
}

public sealed class TotalsBlock
{
    public decimal TotalHT { get; init; }
    public decimal RemiseAmount { get; init; }
    public string? RemiseLabel { get; init; }
    public decimal TotalTVA { get; init; }
    public decimal? FraisPort { get; init; }
    public string? FraisPortLabel { get; init; }
    public decimal? AutresFrais { get; init; }
    public string? AutresFraisLabel { get; init; }
    public decimal TotalTTC { get; init; }
    public decimal MontantPaye { get; init; }
    public decimal SoldeRestant { get; init; }
}

public static class ExportDocumentFactory
{
    public static ExportDocument FromInvoice(Invoice invoice, DocumentStrings strings, byte[]? logo)
    {
        var lines = invoice.Lines
            .OrderBy(l => l.SortOrder)
            .Select((l, i) => new ExportLine
            {
                Index = i + 1,
                Reference = l.Reference,
                Designation = l.Description,
                Quantity = l.Quantity,
                UnitPriceHT = l.UnitPriceHT,
                VatLabel = strings.TvaLabel(l.TVARate),
                TotalHT = l.TotalHT,
                TotalTTC = l.TotalTTC,
            })
            .ToList();

        var vatBreakdowns = invoice.TVABreakdowns
            .OrderByDescending(b => b.TVARate)
            .Select(b => new VatBreakdownBlock
            {
                Label = strings.TvaLabel(b.TVARate),
                BaseHT = b.TotalHT,
                VatAmount = b.TVAAmount,
                Ttc = b.TotalTTC,
            })
            .ToList();

        var company = invoice.Company ?? new Company();
        var client = invoice.Client;

        var remiseLabel = invoice.RemiseAmount > 0m
            ? invoice.RemiseIsPercentage
                ? $"{invoice.RemiseValue:0.##}%"
                : $"{invoice.RemiseAmount:0.##} DA"
            : null;

        return new ExportDocument
        {
            Strings = strings,
            InvoiceType = invoice.InvoiceType,
            Title = strings.TitleFor(invoice.InvoiceType),
            InvoiceNumber = invoice.InvoiceNumber,
            Status = strings.StatusText(invoice.Status),
            StatusColorHex = StatusHex(invoice.Status),
            IssueDate = invoice.InvoiceDate,
            DueDate = invoice.DueDate,
            PaymentMethod = strings.PaymentMethodText(invoice.PaymentMethod),
            OrderReference = invoice.OrderReference,
            Company = new CompanyBlock
            {
                Name = company.CompanyName,
                Address = BuildCompanyAddress(company),
                Phone = string.IsNullOrWhiteSpace(company.Phone) ? string.Empty : company.Phone,
                Email = company.Email,
                NIF = company.NIF,
                NIS = company.NIS,
                RC = company.RC,
                ART = company.ART,
                RIB = company.RIB,
                CCP = company.CCP,
                BankName = company.BankName,
                Logo = logo,
            },
            Client = new PartyBlock
            {
                Name = client?.DisplayName ?? string.Empty,
                Address = BuildClientAddress(client),
                Phone = client?.Phone ?? string.Empty,
                Email = client?.Email ?? string.Empty,
                NIF = client?.NIF ?? string.Empty,
                RC = client?.RC ?? string.Empty,
                ART = client?.ART ?? string.Empty,
            },
            Lines = lines,
            VatBreakdowns = vatBreakdowns,
            Totals = new TotalsBlock
            {
                TotalHT = invoice.TotalHT,
                RemiseAmount = invoice.RemiseAmount,
                RemiseLabel = remiseLabel,
                TotalTVA = invoice.TotalTVA,
                FraisPort = invoice.FraisPort,
                FraisPortLabel = invoice.FraisPortLabel,
                AutresFrais = invoice.AutresFrais,
                AutresFraisLabel = invoice.AutresFraisLabel,
                TotalTTC = invoice.TotalTTC,
                MontantPaye = invoice.MontantPaye,
                SoldeRestant = invoice.SoldeRestant,
            },
            PaymentConditions = invoice.PaymentConditions,
            Penalties = invoice.Penalties,
            MentionsSpecifiques = invoice.MentionsSpecifiques,
            Notes = invoice.Notes,
            AmountInWords = AmountToWords.FormatTotal(invoice.TotalTTC, strings),
        };
    }

    private static string StatusHex(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Payee => "#15803D",
        InvoiceStatus.Annulee => "#B91C1C",
        InvoiceStatus.Finalisee => "#1D4ED8",
        _ => "#92400E",
    };

    public static string BuildCompanyAddress(Company c)
    {
        var parts = new[] { c.Address, c.PostalCode, c.City, c.Wilaya }.Where(x => !string.IsNullOrWhiteSpace(x));
        return string.Join(", ", parts);
    }

    public static string BuildClientAddress(Client? client)
    {
        if (client is null) return string.Empty;
        var parts = new[] { client.Address, client.PostalCode, client.City, client.Wilaya }.Where(x => !string.IsNullOrWhiteSpace(x));
        return string.Join(", ", parts);
    }
}
