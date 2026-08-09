using Factur.Application.DTOs;
using Factur.Application.Validators;
using Factur.Domain.Enums;

namespace Factur.Tests;

public class ValidatorsTests
{
    private static CreateInvoiceRequest ValidInvoice() => new()
    {
        ClientId = Guid.NewGuid(),
        Lines = new List<InvoiceLineRequest>
        {
            new() { Description = "Prestation", Quantity = 1, UnitPriceHT = 100m, TVARate = TVARate.Normal },
        },
    };

    [Fact]
    public void CreateInvoice_SansLigne_EstInvalide()
    {
        var validator = new CreateInvoiceRequestValidator();
        var request = ValidInvoice();
        request.Lines = new List<InvoiceLineRequest>();

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Lines");
    }

    [Fact]
    public void CreateInvoice_LigneSansDescription_EstInvalide()
    {
        var validator = new CreateInvoiceRequestValidator();
        var request = ValidInvoice();
        request.Lines[0].Description = " ";

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Lines[0].Description");
    }

    [Fact]
    public void CreateInvoice_QuantiteNulle_EstInvalide()
    {
        var validator = new CreateInvoiceRequestValidator();
        var request = ValidInvoice();
        request.Lines[0].Quantity = 0;

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void CreateInvoice_Valide_Passe()
    {
        var validator = new CreateInvoiceRequestValidator();
        Assert.True(validator.Validate(ValidInvoice()).IsValid);
    }

    [Fact]
    public void CreateInvoice_RemiseNegative_EstInvalide()
    {
        var validator = new CreateInvoiceRequestValidator();
        var request = ValidInvoice();
        request.RemiseValue = -5m;

        Assert.False(validator.Validate(request).IsValid);
    }

    [Theory]
    [InlineData("099916000000000", true)]
    [InlineData("0999160000000", true)]
    [InlineData("09991600000000", true)]
    [InlineData("09991600000000AA", false)]
    [InlineData("0999160000000AA", false)]
    [InlineData("09991600", false)]
    [InlineData("0999160000000000", false)]
    [InlineData("09991600000000A", false)]
    public void Client_NIF_RespecteLeFormat(string nif, bool expected)
    {
        var validator = new CreateClientRequestValidator();
        var client = new CreateClientRequest { DisplayName = "Client", NIF = nif };
        Assert.Equal(expected, validator.Validate(client).IsValid);
    }

    [Fact]
    public void Client_EmailInvalide_EstRejete()
    {
        var validator = new CreateClientRequestValidator();
        var client = new CreateClientRequest { DisplayName = "Client", Email = "pas-un-email" };
        Assert.False(validator.Validate(client).IsValid);
    }

    [Fact]
    public void Product_ReferenceVide_EstRejetee()
    {
        var validator = new CreateProductRequestValidator();
        var product = new CreateProductRequest { Reference = " ", Name = "Produit", DefaultPrice = 10m, DefaultTVARate = TVARate.Normal };
        Assert.False(validator.Validate(product).IsValid);
    }

    [Fact]
    public void Product_PrixNegatif_EstRejete()
    {
        var validator = new CreateProductRequestValidator();
        var product = new CreateProductRequest { Reference = "REF-1", Name = "Produit", DefaultPrice = -1m, DefaultTVARate = TVARate.Normal };
        Assert.False(validator.Validate(product).IsValid);
    }

    [Fact]
    public void Societe_SansRaisonSociale_EstRejetee()
    {
        var validator = new UpdateCompanyRequestValidator();
        var company = new UpdateCompanyRequest { CompanyName = "", Address = "Cité 200 logements, Bloc A", Phone = "0550123456", Email = "test@company.dz", NIF = "099916000000000", NIS = "099916000000000", RC = "16/00-0000000B00", ART = "0000000000000", InvoicePrefix = "FAC", PaymentConditions = "Paiement comptant." };
        Assert.False(validator.Validate(company).IsValid);
    }

    private static UpdateCompanyRequest ValidCompany() => new()
    {
        CompanyName = "Société Test",
        Address = "Cité 200 logements, Bloc A",
        Phone = "0550123456",
        Email = "test@company.dz",
        NIF = "099916000000000",
        NIS = "099916000000000",
        RC = "16/00-0000000B00",
        ART = "0000000000000",
        InvoicePrefix = "FAC",
        PaymentConditions = "Paiement comptant.",
    };

    [Theory]
    [InlineData("Intérêts de retard : 0.5% par mois.")]
    [InlineData("0.5% par mois")]
    [InlineData("Les pénalités sont de 0.5% par mois.")]
    [InlineData("1% par mois")]
    [InlineData("2.5% par mois")]
    [InlineData("10% par mois")]
    [InlineData("Retard : 1.5%.")]
    [InlineData("0,5% par mois")]
    public void Societe_PenaltiesAvecPourcentage_EstValide(string penalties)
    {
        var validator = new UpdateCompanyRequestValidator();
        var company = ValidCompany();
        company.Penalties = penalties;

        var result = validator.Validate(company);

        Assert.DoesNotContain(result.Errors, e => e.PropertyName == "Penalties");
    }

    [Theory]
    [InlineData("Intérêts de retard")]
    [InlineData("Paiement en retard")]
    [InlineData("0.5 par mois")]
    [InlineData("abc")]
    public void Societe_PenaltiesSansPourcentage_EstInvalide(string penalties)
    {
        var validator = new UpdateCompanyRequestValidator();
        var company = ValidCompany();
        company.Penalties = penalties;

        var result = validator.Validate(company);

        Assert.Contains(result.Errors, e => e.PropertyName == "Penalties");
    }
}
