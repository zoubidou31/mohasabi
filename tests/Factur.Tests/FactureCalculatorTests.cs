using Factur.Domain.Entities;
using Factur.Domain.Enums;
using Factur.Domain.Services;

namespace Factur.Tests;

public class FactureCalculatorTests
{
    private static Invoice BuildInvoice(params (string refe, decimal qty, decimal price, TVARate rate)[] lines)
    {
        var invoice = new Invoice();
        for (var i = 0; i < lines.Length; i++)
        {
            var (refe, qty, price, rate) = lines[i];
            invoice.Lines.Add(new InvoiceLine
            {
                Reference = refe,
                Quantity = qty,
                UnitPriceHT = price,
                TVARate = rate,
                SortOrder = i,
            });
        }

        return invoice;
    }

    [Theory]
    [InlineData(2, 1000.00, 2000.00)]
    [InlineData(3, 333.33, 999.99)]
    [InlineData(0.5, 200.00, 100.00)]
    public void LineHT_CalculeProduitQuantitePrix(decimal qty, decimal price, decimal expected)
    {
        var line = new InvoiceLine { Quantity = qty, UnitPriceHT = price };
        Assert.Equal(expected, FactureCalculator.LineHT(line));
    }

    [Theory]
    [InlineData(TVARate.Normal, 100.00, 19.00)]
    [InlineData(TVARate.Reduit, 200.00, 18.00)]
    [InlineData(TVARate.Exonere, 100.00, 0.00)]
    [InlineData(TVARate.IFU, 100.00, 0.00)]
    public void LineTVA_AppliqueLeBonTaux(TVARate rate, decimal ht, decimal expected)
    {
        Assert.Equal(expected, FactureCalculator.LineTVA(ht, rate));
    }

    [Fact]
    public void LineTTC_EstSommeHTEtTVA()
    {
        Assert.Equal(119.00m, FactureCalculator.LineTTC(100.00m, 19.00m));
    }

    [Theory]
    [InlineData(1.005, false, 1.01)]
    [InlineData(1.005, true, 1.00)]
    [InlineData(2.345, false, 2.35)]
    [InlineData(2.345, true, 2.34)]
    public void Round_RespecteLeModeDArrondi(decimal value, bool bankers, decimal expected)
    {
        Assert.Equal(expected, FactureCalculator.Round(value, bankers));
    }

    [Fact]
    public void RecalculateInvoice_CalculeTotauxMultiTaux()
    {
        var invoice = BuildInvoice(
            ("L1", 2, 1000.00m, TVARate.Normal),
            ("L2", 1, 500.00m, TVARate.Reduit));

        FactureCalculator.RecalculateInvoice(invoice);

        Assert.Equal(2000.00m, invoice.Lines.ElementAt(0).TotalHT);
        Assert.Equal(380.00m, invoice.Lines.ElementAt(0).TVAAmount);
        Assert.Equal(2380.00m, invoice.Lines.ElementAt(0).TotalTTC);
        Assert.Equal(500.00m, invoice.Lines.ElementAt(1).TotalHT);
        Assert.Equal(45.00m, invoice.Lines.ElementAt(1).TVAAmount);

        Assert.Equal(2500.00m, invoice.TotalHT);
        Assert.Equal(425.00m, invoice.TotalTVA);
        Assert.Equal(2925.00m, invoice.TotalTTC);
        Assert.Equal(0m, invoice.RemiseAmount);

        Assert.Equal(2, invoice.TVABreakdowns.Count);
        Assert.Equal(TVARate.Normal, invoice.TVABreakdowns.ElementAt(0).TVARate);
        Assert.Equal(2000.00m, invoice.TVABreakdowns.ElementAt(0).TotalHT);
        Assert.Equal(TVARate.Reduit, invoice.TVABreakdowns.ElementAt(1).TVARate);
        Assert.Equal(500.00m, invoice.TVABreakdowns.ElementAt(1).TotalHT);
    }

    [Fact]
    public void RecalculateInvoice_AppliqueRemisePourcent()
    {
        var invoice = BuildInvoice(("L1", 1, 1000.00m, TVARate.Normal));
        invoice.RemiseValue = 10m;
        invoice.RemiseIsPercentage = true;

        FactureCalculator.RecalculateInvoice(invoice);

        Assert.Equal(100.00m, invoice.RemiseAmount);
        Assert.Equal(900.00m, invoice.TotalHT);
        Assert.Equal(171.00m, invoice.TotalTVA);
        Assert.Equal(1071.00m, invoice.TotalTTC);
    }

    [Fact]
    public void RecalculateInvoice_RemiseFixePlafonneeAuTotal()
    {
        var invoice = BuildInvoice(("L1", 1, 500.00m, TVARate.Normal));
        invoice.RemiseValue = 900m;
        invoice.RemiseIsPercentage = false;

        FactureCalculator.RecalculateInvoice(invoice);

        Assert.Equal(500.00m, invoice.RemiseAmount);
        Assert.Equal(0m, invoice.TotalHT);
        Assert.Equal(0m, invoice.TotalTTC);
    }

    [Fact]
    public void RecalculateInvoice_RemiseRepartieProportionnellementSurLesLignes()
    {
        var invoice = BuildInvoice(
            ("L1", 1, 100.00m, TVARate.Normal),
            ("L2", 1, 900.00m, TVARate.Normal));
        invoice.RemiseValue = 10m;
        invoice.RemiseIsPercentage = true;

        FactureCalculator.RecalculateInvoice(invoice);

        Assert.Equal(90.00m, invoice.Lines.ElementAt(0).TotalHT);
        Assert.Equal(810.00m, invoice.Lines.ElementAt(1).TotalHT);
        Assert.Equal(900.00m, invoice.TotalHT);
    }

    [Fact]
    public void RecalculateInvoice_AjouteFraisAuHT()
    {
        var invoice = BuildInvoice(("L1", 1, 1000.00m, TVARate.Normal));
        invoice.FraisPort = 50.00m;
        invoice.AutresFrais = 20.00m;

        FactureCalculator.RecalculateInvoice(invoice);

        Assert.Equal(1070.00m, invoice.TotalHT);
        Assert.Equal(190.00m, invoice.TotalTVA);
        Assert.Equal(1260.00m, invoice.TotalTTC);
    }

    [Fact]
    public void RecalculateInvoice_LigneIFUProduitTVAZero()
    {
        var invoice = BuildInvoice(("L1", 1, 1000.00m, TVARate.IFU));

        FactureCalculator.RecalculateInvoice(invoice);

        Assert.Equal(0m, invoice.TotalTVA);
        Assert.Equal(1000.00m, invoice.TotalTTC);
        Assert.Single(invoice.TVABreakdowns);
        Assert.Equal(TVARate.IFU, invoice.TVABreakdowns.ElementAt(0).TVARate);
        Assert.Equal("Soumis à l'IFU", invoice.TVABreakdowns.ElementAt(0).TVARate.Label());
    }

    [Fact]
    public void RecalculateInvoice_AucuneLigne_DonneTotauxZero()
    {
        var invoice = BuildInvoice();

        FactureCalculator.RecalculateInvoice(invoice);

        Assert.Equal(0m, invoice.TotalHT);
        Assert.Equal(0m, invoice.TotalTVA);
        Assert.Equal(0m, invoice.TotalTTC);
        Assert.Empty(invoice.TVABreakdowns);
    }

    [Fact]
    public void RecalculateInvoice_ArrondiBancaireRespecte()
    {
        // L'arrondi par ligne reste AwayFromZero ; le mode bancaire s'applique aux totaux.
        var invoice = BuildInvoice(("L1", 1, 1.005m, TVARate.Exonere));

        FactureCalculator.RecalculateInvoice(invoice, bankersRounding: true);

        Assert.Equal(1.01m, invoice.TotalHT);
    }
}
