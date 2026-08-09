using Factur.Domain.Common;
using Factur.Domain.Enums;

namespace Factur.Domain.Entities;

/// <summary>Récapitulatif TVA par taux pour une facture.</summary>
public class TVABreakdown : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public TVARate TVARate { get; set; }
    public decimal TotalHT { get; set; }
    public decimal TVAAmount { get; set; }
    public decimal TotalTTC { get; set; }
    public Invoice? Invoice { get; set; }
}
