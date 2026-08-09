using Factur.Domain.Enums;

namespace Factur.Infrastructure.Export;

/// <summary>Langue cible des documents exportés.</summary>
public enum ExportLanguage
{
    French = 0,
    English = 1,
}

/// <summary>Résout les libellés bilingues (français / anglais) utilisés par les documents exportés.</summary>
public sealed class DocumentStrings
{
    private readonly ExportLanguage _lang;

    public DocumentStrings(ExportLanguage lang) => _lang = lang;

    public static DocumentStrings For(string? lang) =>
        new(string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) ? ExportLanguage.English : ExportLanguage.French);

    public ExportLanguage Language => _lang;
    public bool IsEnglish => _lang == ExportLanguage.English;

    public string T(string fr, string en) => _lang == ExportLanguage.English ? en : fr;

    // ------------------------------------------------------------ titres

    public string Facture => T("FACTURE", "INVOICE");
    public string ProForma => T("PRO-FORMA", "PRO-FORMA");
    public string Avoir => T("AVOIR", "CREDIT NOTE");
    public string Devis => T("DEVIS", "QUOTATION");

    public string TitleFor(InvoiceType type) => type switch
    {
        InvoiceType.ProForma => ProForma,
        InvoiceType.Avoir => Avoir,
        _ => Facture,
    };

    // ------------------------------------------------------------ document

    public string BillTo => T("FACTURÉ À", "BILLED TO");
    public string SoldBy => T("ÉMIS PAR", "ISSUED BY");
    public string Client => T("CLIENT", "CLIENT");
    public string Supplier => T("FOURNISSEUR", "SUPPLIER");
    public string Number => T("N°", "No.");
    public string IssueDate => T("Date d'émission", "Issue date");
    public string DueDate => T("Échéance", "Due date");
    public string PaymentMethodLabel => T("Mode de paiement", "Payment method");
    public string OrderReference => T("Réf. commande", "Order ref.");
    public string StatusLabel => T("Statut", "Status");
    public string Paid => T("Payé", "Paid");
    public string AmountPaid => T("Montant payé", "Amount paid");
    public string BalanceDue => T("Reste à payer", "Balance due");
    public string PaymentDate => T("Date de paiement", "Payment date");

    // ------------------------------------------------------------ table

    public string Index => T("#", "#");
    public string Reference => T("Référence", "Reference");
    public string Designation => T("Désignation", "Description");
    public string Quantity => T("Qté", "Qty");
    public string UnitPrice => T("Prix unitaire HT", "Unit price (excl. tax)");
    public string Vat => T("TVA", "VAT");
    public string AmountHT => T("Montant HT", "Amount (excl. tax)");
    public string AmountTTC => T("Montant TTC", "Amount (incl. tax)");
    public string TotalHT => T("Total HT", "Total (excl. tax)");
    public string TotalTTC => T("TOTAL TTC", "TOTAL INCL. TAX");

    // ------------------------------------------------------------ totaux

    public string Subtotal => T("Sous-total", "Subtotal");
    public string Discount => T("Remise", "Discount");
    public string DiscountDetail => T("Remise", "Discount");
    public string TotalVat => T("Total TVA", "Total VAT");
    public string Including => T("dont", "incl.");
    public string Shipping => T("Frais de port", "Shipping");
    public string OtherFees => T("Autres frais", "Other fees");
    public string VatSummary => T("Récapitulatif TVA", "VAT SUMMARY");
    public string Rate => T("Taux", "Rate");
    public string Base => T("Base HT", "Base (excl. tax)");
    public string VatAmount => T("TVA", "VAT");
    public string Ttc => T("TTC", "Incl. tax");
    public string AmountInWordsLabel => T("Arrêté le présent document à la somme de :", "This document amounts to:");
    public string AmountInWordsDinars => T("dinars algériens", "Algerian dinars");
    public string AmountInWordsCentimes => T("centimes", "centimes");

    // ------------------------------------------------------------ notes

    public string ConditionsAndMentions => T("Conditions et mentions", "Terms and notes");
    public string PaymentConditions => T("Conditions de paiement", "Payment terms");
    public string LatePenalties => T("Pénalités de retard", "Late payment penalties");
    public string Notes => T("Notes", "Notes");

    // ------------------------------------------------------------ footer

    public string GeneratedOn => T("Document généré le", "Document generated on");
    public string Page => T("Page", "Page");
    public string Of => T("sur", "of");

    // ------------------------------------------------------------ statut

    public string StatusBrouillon => T("Brouillon", "Draft");
    public string StatusFinalisee => T("Finalisée", "Finalized");
    public string StatusPayee => T("Payée", "Paid");
    public string StatusAnnulee => T("Annulée", "Cancelled");

    public string StatusText(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Brouillon => StatusBrouillon,
        InvoiceStatus.Finalisee => StatusFinalisee,
        InvoiceStatus.Payee => StatusPayee,
        InvoiceStatus.Annulee => StatusAnnulee,
        _ => status.ToString(),
    };

    public string PaymentMethodText(PaymentMethod method) => method switch
    {
        PaymentMethod.Comptant => T("Comptant", "Cash"),
        PaymentMethod.Cheque => T("Chèque", "Cheque"),
        PaymentMethod.VirementBancaire => T("Virement bancaire", "Bank transfer"),
        PaymentMethod.CarteBancaire => T("Carte bancaire", "Bank card"),
        PaymentMethod.Credit => T("Crédit", "Credit"),
        _ => method.ToString(),
    };

    public string Phone => T("Tél.", "Phone");
    public string Email => T("E-mail", "E-mail");
    public string Address => T("Adresse", "Address");

    // ------------------------------------------------------------ reports

    public string MonthlyReport => T("RAPPORT MENSUEL", "MONTHLY REPORT");
    public string TvaDeclaration => T("DÉCLARATION TVA", "VAT DECLARATION");
    public string UnpaidList => T("LISTE DES IMPAYÉS", "UNPAID INVOICES");
    public string TopClients => T("MEILLEURS CLIENTS", "TOP CLIENTS");
    public string YearlyReport => T("RAPPORT ANNUEL", "YEARLY REPORT");
    public string Period => T("Période", "Period");
    public string InvoiceNumber => T("N° facture", "Invoice no.");
    public string ClientName => T("Client", "Client");
    public string InvoiceDate => T("Date", "Date");
    public string DueDateShort => T("Échéance", "Due date");
    public string Type => T("Type", "Type");
    public string Status => T("Statut", "Status");
    public string Payee => T("Payé", "Paid");
    public string Solde => T("Solde", "Balance");
    public string InvoiceCount => T("Nombre de factures", "Invoice count");
    public string Collected => T("Encaissé", "Collected");
    public string Outstanding => T("Impayé", "Outstanding");
    public string Exempt => T("Exonéré", "Exempt");
    public string Ifu => T("Soumis à l'IFU", "Subject to IFU");
    public string TotalLabel => T("Total", "Total");
    public string GrandTotal => T("TOTAL", "TOTAL");

    public string VatRateLabel(InvoiceType? _) => T("Taux", "Rate");

    public string TvaLabel(TVARate rate) => rate switch
    {
        TVARate.Normal => "19%",
        TVARate.Reduit => "9%",
        TVARate.Exonere => Exempt,
        TVARate.IFU => Ifu,
        _ => $"{rate}%",
    };

    // ------------------------------------------------------------ dates

    private static readonly string[] FrenchMonths =
        { "Janvier", "Février", "Mars", "Avril", "Mai", "Juin", "Juillet", "Août", "Septembre", "Octobre", "Novembre", "Décembre" };

    private static readonly string[] EnglishMonths =
        { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };

    public string MonthName(int month) =>
        month is < 1 or > 12 ? string.Empty : (IsEnglish ? EnglishMonths : FrenchMonths)[month - 1];
}
