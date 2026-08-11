using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Factur.Tests;

public class ApiIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Api_RepondSansAuthentification()
    {
        var response = await _client.GetAsync("/api/clients");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Societe_EstSeedee()
    {
        var response = await _client.GetAsync("/api/company");
        response.EnsureSuccessStatusCode();

        var company = await response.Content.ReadFromJsonAsync<CompanyDto>(Json);
        Assert.NotNull(company);
        Assert.Equal("Ma Société", company.CompanyName);
        Assert.False(string.IsNullOrWhiteSpace(company.InvoicePrefix));
    }

    [Fact]
    public async Task CycleDeVie_FactureComplete()
    {
        var clientId = await EnsureClientIdAsync();

        // 1. Création brouillon
        var create = await SendAsync(HttpMethod.Post, "/api/invoices", new
        {
            clientId,
            invoiceType = "Facture",
            paymentMethod = "Comptant",
            lines = new[]
            {
                new { description = "Prestation developpement", quantity = 2m, unitPriceHT = 1000m, tvaRate = "Normal" },
                new { description = "Maintenance mensuelle", quantity = 1m, unitPriceHT = 500m, tvaRate = "Reduit" },
            },
        });
        create.EnsureSuccessStatusCode();
        var invoice = (await create.Content.ReadFromJsonAsync<InvoiceDto>(Json))!;

        Assert.Equal("Brouillon", invoice.Status.ToString());
        Assert.Equal(2500m, invoice.TotalHT);
        Assert.Equal(425m, invoice.TotalTVA);
        Assert.Equal(2925m, invoice.TotalTTC);
        Assert.Equal(2, invoice.Lines.Count);
        Assert.Equal(2, invoice.TVABreakdowns.Count);

        // 2. Finalisation
        var finalized = (await (await SendAsync(HttpMethod.Post, $"/api/invoices/{invoice.Id}/finalize", null))
            .Content.ReadFromJsonAsync<InvoiceDto>(Json))!;
        Assert.Equal("Finalisee", finalized.Status.ToString());
        Assert.False(string.IsNullOrWhiteSpace(finalized.InvoiceNumber));

        // 3. Paiement intégral
        var payed = (await (await SendAsync(HttpMethod.Post, $"/api/invoices/{invoice.Id}/pay", new { amount = (decimal?)null }))
            .Content.ReadFromJsonAsync<InvoiceDto>(Json))!;
        Assert.Equal("Payee", payed.Status.ToString());
        Assert.Equal(2925m, payed.MontantPaye);
        Assert.Equal(0m, payed.SoldeRestant);

        // 4. Une facture payée ne peut plus être modifiée
        var forbiddenEdit = await SendAsync(HttpMethod.Post, $"/api/invoices/{invoice.Id}/finalize", null);
        Assert.Equal(HttpStatusCode.BadRequest, forbiddenEdit.StatusCode);
    }

    [Fact]
    public async Task Facture_TotalementPayee_NePeutPlusRecevoirDePaiement()
    {
        var id = await CreateDraftAsync();
        Assert.True((await SendAsync(HttpMethod.Post, $"/api/invoices/{id}/finalize", null)).IsSuccessStatusCode);
        Assert.True((await SendAsync(HttpMethod.Post, $"/api/invoices/{id}/pay", new { amount = (decimal?)null })).IsSuccessStatusCode);

        var extra = await SendAsync(HttpMethod.Post, $"/api/invoices/{id}/pay", new { amount = 100m });
        Assert.Equal(HttpStatusCode.BadRequest, extra.StatusCode);
    }

    [Fact]
    public async Task Facture_SansLignes_EstRefusee()
    {
        var response = await SendAsync(HttpMethod.Post, "/api/invoices", new
        {
            clientId = Guid.NewGuid(),
            invoiceType = "Facture",
            lines = Array.Empty<object>(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Avoir_SurFacture_ProduitMontantNegatif()
    {
        var id = await CreateDraftAsync();
        var credit = await SendAsync(HttpMethod.Post, $"/api/invoices/{id}/credit-note", null);
        credit.EnsureSuccessStatusCode();

        var avoir = (await credit.Content.ReadFromJsonAsync<InvoiceDto>(Json))!;
        Assert.Equal("Avoir", avoir.InvoiceType.ToString());
        Assert.True(avoir.TotalTTC < 0m);
    }

    [Fact]
    public async Task ExportPdf_RetourneUnDocumentValide()
    {
        var id = await CreateDraftAsync();
        var response = await SendAsync(HttpMethod.Get, $"/api/invoices/{id}/export/pdf", null);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 1000);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
    }

    // ---------------------------------------------------------------- helpers

    private async Task<Guid> EnsureClientIdAsync()
    {
        var response = await _client.GetAsync("/api/clients");
        var paged = (await response.Content.ReadFromJsonAsync<PagedResultDto<ClientDto>>(Json))!;
        if (paged.Items.Count > 0)
        {
            return paged.Items[0].Id;
        }

        var create = await _client.PostAsJsonAsync("/api/clients", new
        {
            displayName = $"Client Test {Guid.NewGuid():N}"[..20],
            type = "Entreprise",
            nif = "099916000000013",
            phone = "0550123456",
            address = "Cité 20 Août 1956, Alger",
        });
        create.EnsureSuccessStatusCode();
        return await create.Content.ReadFromJsonAsync<Guid>(Json);
    }

    private async Task<Guid> CreateDraftAsync()
    {
        var clientId = await EnsureClientIdAsync();
        var create = await SendAsync(HttpMethod.Post, "/api/invoices", new
        {
            clientId,
            invoiceType = "Facture",
            paymentMethod = "Comptant",
            lines = new[]
            {
                new { description = "Prestation", quantity = 1m, unitPriceHT = 100m, tvaRate = "Normal" },
            },
        });
        create.EnsureSuccessStatusCode();
        return (await create.Content.ReadFromJsonAsync<InvoiceDto>(Json))!.Id;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string uri, object? body)
    {
        var request = new HttpRequestMessage(method, uri);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request);
    }

    // ---------------------------------------------------------------- DTOs

    private sealed record ClientDto(Guid Id, string DisplayName);

    private sealed record PagedResultDto<T>(List<T> Items, int TotalCount, int Page, int PageSize);

    private sealed record CompanyDto(string CompanyName, string InvoicePrefix);

    private sealed record InvoiceDto(Guid Id, string InvoiceNumber, decimal TotalHT, decimal TotalTVA, decimal TotalTTC,
        decimal MontantPaye, decimal SoldeRestant, object Status, object InvoiceType,
        List<InvoiceLineDto> Lines, List<TVABreakdownDto> TVABreakdowns);

    private sealed record InvoiceLineDto(Guid? Id, string Description, decimal TotalHT, decimal TVAAmount, decimal TotalTTC);

    private sealed record TVABreakdownDto(object TVARate, decimal TotalHT, decimal TVAAmount, decimal TotalTTC);
}
