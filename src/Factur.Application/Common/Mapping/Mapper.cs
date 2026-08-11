using Factur.Application.DTOs;
using Factur.Domain.Entities;
using Factur.Domain.Services;

namespace Factur.Application.Common.Mapping;

/// <summary>Conversions entités → DTO (manuel, sans reflexion).</summary>
public static class Mapper
{
    public static ClientDto ToDto(this Client client, int invoiceCount = 0, decimal totalSpent = 0, decimal outstanding = 0, DateTime? lastInvoiceDate = null) => new()
    {
        Id = client.Id,
        DisplayName = client.DisplayName,
        CompanyName = client.CompanyName,
        Sector = client.Sector,
        NIF = client.NIF,
        RC = client.RC,
        ART = client.ART,
        Address = client.Address,
        PostalCode = client.PostalCode,
        City = client.City,
        Wilaya = client.Wilaya,
        Phone = client.Phone,
        Mobile = client.Mobile,
        Email = client.Email,
        Type = client.Type,
        DefaultPaymentMethod = client.DefaultPaymentMethod,
        Notes = client.Notes,
        CreatedDate = client.CreatedDate,
        IsActive = client.IsActive,
        InvoiceCount = invoiceCount,
        TotalSpent = totalSpent,
        Outstanding = outstanding,
        LastInvoiceDate = lastInvoiceDate,
    };

    public static ProductDto ToDto(this Product product) => new()
    {
        Id = product.Id,
        Reference = product.Reference,
        Name = product.Name,
        Description = product.Description,
        Category = product.Category,
        CategoryId = product.CategoryId,
        CategoryName = product.CategoryRef?.Name,
        DefaultPrice = product.DefaultPrice,
        DefaultTVARate = product.DefaultTVARate,
        IsService = product.IsService,
        IsActive = product.IsActive,
    };

    public static CategoryDto ToDto(this Category category) => new()
    {
        Id = category.Id,
        Name = category.Name,
        Description = category.Description,
        IsActive = category.IsActive,
    };

    public static CompanyDto ToDto(this Company company) => new()
    {
        Id = company.Id,
        CompanyName = company.CompanyName,
        LogoPath = company.LogoPath,
        Address = company.Address,
        PostalCode = company.PostalCode,
        City = company.City,
        Wilaya = company.Wilaya,
        Phone = company.Phone,
        Mobile = company.Mobile,
        Email = company.Email,
        NIF = company.NIF,
        NIS = company.NIS,
        RC = company.RC,
        ART = company.ART,
        RIB = company.RIB,
        CCP = company.CCP,
        BankName = company.BankName,
        InvoicePrefix = company.InvoicePrefix,
        InvoiceSerie = company.InvoiceSerie,
        ValidityDays = company.ValidityDays,
        DefaultTVARate = company.DefaultTVARate,
        PaymentConditions = company.PaymentConditions,
        Penalties = company.Penalties,
        BankAccountNumber = company.BankAccountNumber,
        StampPath = company.StampPath,
        UseBankersRounding = company.UseBankersRounding,
    };

    public static InvoiceSummaryDto ToSummaryDto(this Invoice invoice) => new()
    {
        Id = invoice.Id,
        InvoiceNumber = invoice.InvoiceNumber,
        ClientName = invoice.Client?.DisplayName ?? string.Empty,
        InvoiceDate = invoice.InvoiceDate,
        DueDate = invoice.DueDate,
        InvoiceType = invoice.InvoiceType,
        Status = invoice.Status,
        TotalHT = invoice.TotalHT,
        TotalTVA = invoice.TotalTVA,
        TotalTTC = invoice.TotalTTC,
        MontantPaye = invoice.MontantPaye,
        SoldeRestant = invoice.SoldeRestant,
        IsOverdue = invoice.IsOverdue,
    };

    public static InvoiceLineDto ToDto(this InvoiceLine line) => new()
    {
        Id = line.Id,
        ProductId = line.ProductId,
        Reference = line.Reference,
        Description = line.Description,
        Quantity = line.Quantity,
        UnitPriceHT = line.UnitPriceHT,
        TVARate = line.TVARate,
        TotalHT = line.TotalHT,
        TVAAmount = line.TVAAmount,
        TotalTTC = line.TotalTTC,
        SortOrder = line.SortOrder,
    };

    public static TVABreakdownDto ToDto(this TVABreakdown b) => new()
    {
        TVARate = b.TVARate,
        TotalHT = b.TotalHT,
        TVAAmount = b.TVAAmount,
        TotalTTC = b.TotalTTC,
    };

    public static PaymentDto ToDto(this Payment p) => new()
    {
        Id = p.Id,
        PaymentDate = p.PaymentDate,
        Amount = p.Amount,
        PaymentMethod = p.PaymentMethod,
        ChequeNumber = p.ChequeNumber,
        Notes = p.Notes,
    };

    public static InvoiceDto ToFullDto(this Invoice invoice)
    {
        var summary = invoice.ToSummaryDto();
        return new InvoiceDto
        {
            Id = summary.Id,
            InvoiceNumber = summary.InvoiceNumber,
            ClientName = summary.ClientName,
            InvoiceDate = summary.InvoiceDate,
            DueDate = summary.DueDate,
            InvoiceType = summary.InvoiceType,
            Status = summary.Status,
            TotalHT = summary.TotalHT,
            TotalTVA = summary.TotalTVA,
            TotalTTC = summary.TotalTTC,
            MontantPaye = summary.MontantPaye,
            SoldeRestant = summary.SoldeRestant,
            IsOverdue = summary.IsOverdue,
            ClientId = invoice.ClientId,
            CompanyId = invoice.CompanyId,
            Sequence = invoice.Sequence,
            PaymentMethod = invoice.PaymentMethod,
            ChequeNumber = invoice.ChequeNumber,
            OrderReference = invoice.OrderReference,
            BonCommande = invoice.BonCommande,
            Notes = invoice.Notes,
            MentionsSpecifiques = invoice.MentionsSpecifiques,
            PaymentConditions = invoice.PaymentConditions,
            Penalties = invoice.Penalties,
            RemiseValue = invoice.RemiseValue,
            RemiseIsPercentage = invoice.RemiseIsPercentage,
            RemiseAmount = invoice.RemiseAmount,
            FraisPort = invoice.FraisPort,
            FraisPortLabel = invoice.FraisPortLabel,
            AutresFrais = invoice.AutresFrais,
            AutresFraisLabel = invoice.AutresFraisLabel,
            CreditNoteForInvoiceId = invoice.CreditNoteForInvoiceId,
            CreatedBy = invoice.CreatedBy,
            CreatedDate = invoice.CreatedDate,
            FinalizedDate = invoice.FinalizedDate,
            PaidDate = invoice.PaidDate,
            CancelledDate = invoice.CancelledDate,
            Client = invoice.Client?.ToDto(),
            Lines = invoice.Lines.OrderBy(l => l.SortOrder).Select(l => l.ToDto()).ToList(),
            TVABreakdowns = invoice.TVABreakdowns.OrderByDescending(b => b.TVARate).Select(b => b.ToDto()).ToList(),
            Payments = invoice.Payments.OrderByDescending(p => p.PaymentDate).Select(p => p.ToDto()).ToList(),
        };
    }
}
