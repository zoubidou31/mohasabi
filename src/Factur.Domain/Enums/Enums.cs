namespace Factur.Domain.Enums;

/// <summary>Rôle d'un utilisateur dans l'application.</summary>
public enum UserRole
{
    /// <summary>Accès complet : gestion des utilisateurs, société, suppression.</summary>
    Administrateur = 0,

    /// <summary>Gestion comptable : factures, clients, produits, rapports.</summary>
    Comptable = 1,

    /// <summary>Accès limité : consultation et création, sans suppression.</summary>
    Utilisateur = 2,
}

/// <summary>Type de client (acheteur).</summary>
public enum ClientType
{
    Entreprise = 0,
    Particulier = 1,
    ProfessionnelLiberal = 2,
}

/// <summary>Type de document émis.</summary>
public enum InvoiceType
{
    /// <summary>Facture standard.</summary>
    Facture = 0,

    /// <summary>Facture pro-forma (devis).</summary>
    ProForma = 1,

    /// <summary>Facture d'avoir (note de crédit).</summary>
    Avoir = 2,
}

/// <summary>Cycle de vie d'une facture.</summary>
public enum InvoiceStatus
{
    /// <summary>Modifiable, non soumise.</summary>
    Brouillon = 0,

    /// <summary>Validée, non modifiable, archivable.</summary>
    Finalisee = 1,

    /// <summary>Réglée en totalité.</summary>
    Payee = 2,

    /// <summary>Annulée.</summary>
    Annulee = 3,
}

/// <summary>Mode de paiement.</summary>
public enum PaymentMethod
{
    Comptant = 0,
    Cheque = 1,
    VirementBancaire = 2,
    CarteBancaire = 3,
    Credit = 4,
}

/// <summary>Taux de TVA algérien appliqué à une ligne.</summary>
public enum TVARate
{
    /// <summary>TVA normale : 19%.</summary>
    Normal = 19,

    /// <summary>TVA réduite : 9%.</summary>
    Reduit = 9,

    /// <summary>Exonéré : 0%.</summary>
    Exonere = 0,

    /// <summary>Régime IFU : TVA non applicable, affichage "Soumis à l'IFU".</summary>
    IFU = -1,
}

public static class TVARateExtensions
{
    /// <summary>Retourne le pourcentage applicable (0 pour IFU).</summary>
    public static decimal Percent(this TVARate rate) => rate == TVARate.IFU ? 0m : (decimal)rate;

    /// <summary>Libellé affichable du taux.</summary>
    public static string Label(this TVARate rate) => rate switch
    {
        TVARate.Normal => "19%",
        TVARate.Reduit => "9%",
        TVARate.Exonere => "Exonéré",
        TVARate.IFU => "Soumis à l'IFU",
        _ => $"{rate}%",
    };
}
