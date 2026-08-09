using Factur.Domain.Enums;

namespace Factur.Application.DTOs;

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = new List<T>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
}

public class InvoiceLineDto
{
    public Guid? Id { get; set; }
    public Guid? ProductId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPriceHT { get; set; }
    public TVARate TVARate { get; set; }
    public decimal TotalHT { get; set; }
    public decimal TVAAmount { get; set; }
    public decimal TotalTTC { get; set; }
    public int SortOrder { get; set; }
}

public class TVABreakdownDto
{
    public TVARate TVARate { get; set; }
    public string Label => TVARate.Label();
    public decimal TotalHT { get; set; }
    public decimal TVAAmount { get; set; }
    public decimal TotalTTC { get; set; }
}

public class PaymentDto
{
    public Guid Id { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? ChequeNumber { get; set; }
    public string? Notes { get; set; }
}

public class CreateInvoiceRequest
{
    public Guid ClientId { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public int ValidityDays { get; set; } = 30;
    public InvoiceType InvoiceType { get; set; } = InvoiceType.Facture;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Comptant;
    public string? ChequeNumber { get; set; }
    public string? OrderReference { get; set; }
    public string? BonCommande { get; set; }
    public string? Notes { get; set; }
    public string? MentionsSpecifiques { get; set; }
    public string? PaymentConditions { get; set; }
    public string? Penalties { get; set; }
    public decimal? RemiseValue { get; set; }
    public bool RemiseIsPercentage { get; set; } = true;
    public decimal? FraisPort { get; set; }
    public string? FraisPortLabel { get; set; }
    public decimal? AutresFrais { get; set; }
    public string? AutresFraisLabel { get; set; }
    public List<InvoiceLineRequest> Lines { get; set; } = new();
}

public class UpdateInvoiceRequest : CreateInvoiceRequest
{
    public Guid Id { get; set; }
}

public class InvoiceLineRequest
{
    public Guid? ProductId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1m;
    public decimal UnitPriceHT { get; set; }
    public TVARate TVARate { get; set; } = TVARate.Normal;
}

public class ImportLineRequest
{
    public string Reference { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1m;
    public decimal UnitPriceHT { get; set; }
    public TVARate? TVARate { get; set; }
}

public class InvoiceSummaryDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public InvoiceType InvoiceType { get; set; }
    public InvoiceStatus Status { get; set; }
    public decimal TotalHT { get; set; }
    public decimal TotalTVA { get; set; }
    public decimal TotalTTC { get; set; }
    public decimal MontantPaye { get; set; }
    public decimal SoldeRestant { get; set; }
    public bool IsOverdue { get; set; }
}

public class InvoiceDto : InvoiceSummaryDto
{
    public Guid ClientId { get; set; }
    public Guid CompanyId { get; set; }
    public int Sequence { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? ChequeNumber { get; set; }
    public string? OrderReference { get; set; }
    public string? BonCommande { get; set; }
    public string? Notes { get; set; }
    public string? MentionsSpecifiques { get; set; }
    public string? PaymentConditions { get; set; }
    public string? Penalties { get; set; }
    public decimal? RemiseValue { get; set; }
    public bool RemiseIsPercentage { get; set; }
    public decimal RemiseAmount { get; set; }
    public decimal? FraisPort { get; set; }
    public string? FraisPortLabel { get; set; }
    public decimal? AutresFrais { get; set; }
    public string? AutresFraisLabel { get; set; }
    public Guid? CreditNoteForInvoiceId { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? FinalizedDate { get; set; }
    public DateTime? PaidDate { get; set; }
    public DateTime? CancelledDate { get; set; }
    public ClientDto? Client { get; set; }
    public List<InvoiceLineDto> Lines { get; set; } = new();
    public List<TVABreakdownDto> TVABreakdowns { get; set; } = new();
    public List<PaymentDto> Payments { get; set; } = new();
}

public class InvoiceQuery
{
    public string? Search { get; set; }
    public Guid? ClientId { get; set; }
    public InvoiceStatus? Status { get; set; }
    public InvoiceType? InvoiceType { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public bool? Overdue { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class MarkPaidRequest
{
    public DateTime? PaymentDate { get; set; }
    public decimal? Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Comptant;
    public string? ChequeNumber { get; set; }
    public string? Notes { get; set; }
}

public class PaymentRequest
{
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Comptant;
    public string? ChequeNumber { get; set; }
    public string? Notes { get; set; }
}
