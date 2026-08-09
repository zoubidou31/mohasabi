using Factur.Domain.Common;
using Factur.Domain.Enums;

namespace Factur.Domain.Entities;

/// <summary>Paiement (total ou partiel) enregistré sur une facture.</summary>
public class Payment : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? ChequeNumber { get; set; }
    public string? Notes { get; set; }
    public Invoice? Invoice { get; set; }
}
