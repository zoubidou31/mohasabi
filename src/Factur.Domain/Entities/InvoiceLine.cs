using Factur.Domain.Common;
using Factur.Domain.Enums;

namespace Factur.Domain.Entities;

/// <summary>Ligne de facture.</summary>
public class InvoiceLine : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public Guid? ProductId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1m;
    public decimal UnitPriceHT { get; set; }
    public TVARate TVARate { get; set; } = TVARate.Normal;
    public decimal TotalHT { get; set; }
    public decimal TVAAmount { get; set; }
    public decimal TotalTTC { get; set; }
    public int SortOrder { get; set; }
}
