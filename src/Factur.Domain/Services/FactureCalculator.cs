using Factur.Domain.Entities;
using Factur.Domain.Enums;

namespace Factur.Domain.Services;

/// <summary>
/// Moteur de calcul des factures conforme à la norme algérienne.
/// Arrondi à 2 décimales, remise proportionnelle avant TVA, récapitulatif TVA par taux.
/// </summary>
public static class FactureCalculator
{
    /// <summary>Arrondi à deux décimales (banker's rounding paramétrable).</summary>
    public static decimal Round(decimal value, bool bankersRounding = false)
    {
        var mode = bankersRounding ? MidpointRounding.ToEven : MidpointRounding.AwayFromZero;
        return Math.Round(value, 2, mode);
    }

    /// <summary>HT d'une ligne = Quantité × Prix unitaire HT.</summary>
    public static decimal LineHT(InvoiceLine line) => Round(line.Quantity * line.UnitPriceHT);

    /// <summary>TVA d'une ligne = HT × taux / 100.</summary>
    public static decimal LineTVA(decimal lineHT, TVARate rate) => Round(lineHT * rate.Percent() / 100m);

    /// <summary>TTC d'une ligne = HT + TVA.</summary>
    public static decimal LineTTC(decimal lineHT, decimal lineTVA) => Round(lineHT + lineTVA);

    /// <summary>
    /// Calcule et applique tous les totaux d'une facture :
    /// totaux par ligne, remise proportionnelle, TVA, frais et récapitulatif TVA.
    /// </summary>
    public static void RecalculateInvoice(Invoice invoice, bool bankersRounding = false)
    {
        // 1. Totaux bruts des lignes (avant remise)
        var totalHTBrut = Round(invoice.Lines.Sum(l => LineHT(l)), bankersRounding);

        // 2. Remise globale (pourcentage sur le total HT des lignes, sinon montant fixe)
        var remiseAmount = 0m;
        if (invoice.RemiseValue is > 0m && totalHTBrut > 0m)
        {
            remiseAmount = invoice.RemiseIsPercentage
                ? Round(totalHTBrut * invoice.RemiseValue.Value / 100m, bankersRounding)
                : Round(Math.Min(invoice.RemiseValue.Value, totalHTBrut), bankersRounding);
        }

        var totalHTNetLignes = Round(totalHTBrut - remiseAmount, bankersRounding);

        // 3. Répartition proportionnelle de la remise puis calcul TVA/TTC par ligne
        var totalHTNet = 0m;
        var totalTVA = 0m;
        foreach (var line in invoice.Lines)
        {
            var lineHT = LineHT(line);
            if (lineHT > 0m && remiseAmount > 0m && totalHTBrut > 0m)
            {
                lineHT = Round(lineHT * totalHTNetLignes / totalHTBrut, bankersRounding);
            }

            var lineTVA = LineTVA(lineHT, line.TVARate);
            line.TotalHT = lineHT;
            line.TVAAmount = lineTVA;
            line.TotalTTC = LineTTC(lineHT, lineTVA);

            totalHTNet += lineHT;
            totalTVA += lineTVA;
        }

        // 4. Frais de port / autres frais ajoutés au HT (sans TVA)
        var fraisTotal = Round((invoice.FraisPort ?? 0m) + (invoice.AutresFrais ?? 0m), bankersRounding);

        invoice.RemiseAmount = Round(remiseAmount, bankersRounding);
        invoice.TotalHT = Round(totalHTNet + fraisTotal, bankersRounding);
        invoice.TotalTVA = Round(totalTVA, bankersRounding);
        invoice.TotalTTC = Round(invoice.TotalHT + invoice.TotalTVA, bankersRounding);

        // 5. Récapitulatif TVA par taux
        invoice.TVABreakdowns.Clear();
        foreach (var group in invoice.Lines
            .GroupBy(l => l.TVARate)
            .OrderByDescending(g => g.Key))
        {
            invoice.TVABreakdowns.Add(new TVABreakdown
            {
                TVARate = group.Key,
                TotalHT = Round(group.Sum(l => l.TotalHT), bankersRounding),
                TVAAmount = Round(group.Sum(l => l.TVAAmount), bankersRounding),
                TotalTTC = Round(group.Sum(l => l.TotalTTC), bankersRounding),
            });
        }
    }
}
