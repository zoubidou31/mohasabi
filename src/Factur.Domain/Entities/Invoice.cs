using Factur.Domain.Common;
using Factur.Domain.Enums;

namespace Factur.Domain.Entities;

/// <summary>Facture (ou avoir / pro-forma).</summary>
public class Invoice : BaseEntity
{
    /// <summary>Numéro de facture auto-incrémenté : FAC-YYYY-MM-XXXXXX.</summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>Séquence annuelle/mensuelle ayant généré le numéro.</summary>
    public int Sequence { get; set; }

    public Guid ClientId { get; set; }
    public Guid CompanyId { get; set; }

    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public int ValidityDays { get; set; } = 30;

    public InvoiceType InvoiceType { get; set; } = InvoiceType.Facture;
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Brouillon;

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Comptant;
    public string? ChequeNumber { get; set; }

    public string? OrderReference { get; set; }
    public string? BonCommande { get; set; }
    public string? Notes { get; set; }
    public string? MentionsSpecifiques { get; set; }
    public string? PaymentConditions { get; set; }
    public string? Penalties { get; set; }

    // Remise et frais
    public decimal? RemiseValue { get; set; }
    public bool RemiseIsPercentage { get; set; } = true;
    public decimal? FraisPort { get; set; }
    public string? FraisPortLabel { get; set; }
    public decimal? AutresFrais { get; set; }
    public string? AutresFraisLabel { get; set; }

    // Totaux (calculés)
    public decimal TotalHT { get; set; }
    public decimal TotalTVA { get; set; }
    public decimal TotalTTC { get; set; }
    public decimal RemiseAmount { get; set; }
    public decimal MontantPaye { get; set; }
    public decimal SoldeRestant => Math.Max(0m, TotalTTC - MontantPaye);

    // Avoir / lien
    public Guid? CreditNoteForInvoiceId { get; set; }

    // Audit
    public Guid? CreatedBy { get; set; }
    public DateTime? FinalizedDate { get; set; }
    public DateTime? PaidDate { get; set; }
    public DateTime? CancelledDate { get; set; }

    // Navigation
    public Client? Client { get; set; }
    public Company? Company { get; set; }
    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
    public ICollection<TVABreakdown> TVABreakdowns { get; set; } = new List<TVABreakdown>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public bool IsOverdue =>
        Status != InvoiceStatus.Payee &&
        Status != InvoiceStatus.Annulee &&
        DueDate.HasValue &&
        DueDate.Value.Date < DateTime.UtcNow.Date;
}
