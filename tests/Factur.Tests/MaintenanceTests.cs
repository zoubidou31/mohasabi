using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Factur.Application.DTOs;
using Factur.Application.Validators;
using Factur.Infrastructure.Services;
using Microsoft.Extensions.Configuration;

namespace Factur.Tests;

/// <summary>
/// Tests ciblés pour la passe de maintenance v1.0.1 : concurrence de la
/// numérotation, pagination des impayés, validation des numéros de téléphone
/// algériens, intégrité des catégories et persistance de la préférence « écran
/// de démarrage ».
/// </summary>
public class MaintenanceTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public MaintenanceTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ---------------------------------------------------------------- concurrence

    [Fact]
    public async Task CreationConcurrente_Factures_NumeroUnique()
    {
        var clientId = await EnsureClientIdAsync();

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => SendAsync(HttpMethod.Post, "/api/invoices", new
            {
                clientId,
                invoiceDate = DateTime.UtcNow.Date,
                validityDays = 30,
                invoiceType = "Facture",
                paymentMethod = "Comptant",
                lines = new[]
                {
                    new { description = "Prestation concurrente", quantity = 1m, unitPriceHT = 100m, tvaRate = "Normal" },
                },
            }))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        var invoices = new List<InvoiceCreated>();
        foreach (var response in responses)
        {
            Assert.True(response.IsSuccessStatusCode,
                $"Création concurrente refusée : {response.StatusCode} -> {await response.Content.ReadAsStringAsync()}");
            invoices.Add((await response.Content.ReadFromJsonAsync<InvoiceCreated>(Json))!);
        }

        Assert.Equal(10, invoices.Count);
        Assert.Equal(10, invoices.Select(i => i.InvoiceNumber).Distinct().Count());
        Assert.All(invoices, i => Assert.False(string.IsNullOrWhiteSpace(i.InvoiceNumber)));
    }

    // ---------------------------------------------------------------- rapports impayés

    [Fact]
    public async Task Impayes_Pages_RetourneItemsEtTotaux()
    {
        var clientId = await EnsureClientIdAsync();

        // 3 factures échues et non réglées (Finalisee, échéance dépassée).
        for (var i = 0; i < 3; i++)
        {
            var draft = (await (await SendAsync(HttpMethod.Post, "/api/invoices", new
            {
                clientId,
                invoiceDate = DateTime.UtcNow.AddDays(-40).Date,
                validityDays = 30,
                invoiceType = "Facture",
                paymentMethod = "Comptant",
                lines = new[]
                {
                    new { description = $"Impayé {i}", quantity = 1m, unitPriceHT = 100m, tvaRate = "Normal" },
                },
            })).Content.ReadFromJsonAsync<InvoiceCreated>(Json))!;
            Assert.True((await SendAsync(HttpMethod.Post, $"/api/invoices/{draft.Id}/finalize", null)).IsSuccessStatusCode);
        }

        // 1 brouillon (exclu), 1 payée (exclue), 1 non échue (exclue).
        var paid = (await (await SendAsync(HttpMethod.Post, "/api/invoices", new
        {
            clientId,
            invoiceDate = DateTime.UtcNow.AddDays(-40).Date,
            validityDays = 30,
            invoiceType = "Facture",
            paymentMethod = "Comptant",
            lines = new[]
            {
                new { description = "Payée", quantity = 1m, unitPriceHT = 100m, tvaRate = "Normal" },
            },
        })).Content.ReadFromJsonAsync<InvoiceCreated>(Json))!;
        Assert.True((await SendAsync(HttpMethod.Post, $"/api/invoices/{paid.Id}/finalize", null)).IsSuccessStatusCode);
        var payResp = await SendAsync(HttpMethod.Post, $"/api/invoices/{paid.Id}/pay", new { amount = (decimal?)null });
        Assert.True(payResp.IsSuccessStatusCode, $"Paiement refusé : {payResp.StatusCode} -> {await payResp.Content.ReadAsStringAsync()}");
        Assert.True((await SendAsync(HttpMethod.Post, "/api/invoices", new
        {
            clientId,
            invoiceDate = DateTime.UtcNow.AddDays(-40).Date,
            validityDays = 30,
            invoiceType = "Facture",
            paymentMethod = "Comptant",
            lines = new[]
            {
                new { description = "Brouillon exclu", quantity = 1m, unitPriceHT = 100m, tvaRate = "Normal" },
            },
        })).IsSuccessStatusCode);

        Assert.True((await SendAsync(HttpMethod.Post, "/api/invoices", new
        {
            clientId,
            invoiceDate = DateTime.UtcNow.Date,
            validityDays = 30,
            invoiceType = "Facture",
            paymentMethod = "Comptant",
            lines = new[]
            {
                new { description = "Non échue", quantity = 1m, unitPriceHT = 100m, tvaRate = "Normal" },
            },
        })).IsSuccessStatusCode);

        var page1 = (await (await SendAsync(HttpMethod.Get, "/api/reports/unpaid/paged?page=1&pageSize=2", null))
            .Content.ReadFromJsonAsync<PagedResultDto<UnpaidItem>>(Json))!;
        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(1, page1.Page);
        Assert.Equal(2, page1.PageSize);
        Assert.Equal(2, page1.TotalPages);
        Assert.All(page1.Items, i => Assert.True(i.SoldeRestant > 0m));

        var page2 = (await (await SendAsync(HttpMethod.Get, "/api/reports/unpaid/paged?page=2&pageSize=2", null))
            .Content.ReadFromJsonAsync<PagedResultDto<UnpaidItem>>(Json))!;
        Assert.Equal(3, page2.TotalCount);
        Assert.Single(page2.Items);
    }

    // ---------------------------------------------------------------- catégories

    [Fact]
    public async Task Categorie_NomDuplique_Refuse()
    {
        var name = $"Catégorie {Guid.NewGuid():N}"[..16];
        var first = await SendAsync(HttpMethod.Post, "/api/categories", new { name });
        first.EnsureSuccessStatusCode();

        var dup = await SendAsync(HttpMethod.Post, "/api/categories", new { name });
        Assert.Equal(HttpStatusCode.BadRequest, dup.StatusCode);
    }

    // ---------------------------------------------------------------- validation clients

    [Theory]
    [InlineData("0550123456")]
    [InlineData("0660123456")]
    [InlineData("0770123456")]
    [InlineData("550123456")]
    [InlineData("660123456")]
    public void Client_TelephoneAlgerien_Valide_Passe(string phone)
    {
        var validator = new CreateClientRequestValidator();
        var request = ValidClient();
        request.Phone = phone;

        Assert.True(validator.Validate(request).IsValid);
    }

    [Theory]
    [InlineData("05512345")]
    [InlineData("1550123456")]
    [InlineData("0214567890")]
    [InlineData("abc")]
    public void Client_TelephoneInvalide_EstRefuse(string phone)
    {
        var validator = new CreateClientRequestValidator();
        var request = ValidClient();
        request.Phone = phone;

        Assert.False(validator.Validate(request).IsValid);
    }

    [Fact]
    public void Client_TelephoneVide_EstRefuse()
    {
        var validator = new CreateClientRequestValidator();
        var request = ValidClient();
        request.Phone = "";

        Assert.False(validator.Validate(request).IsValid);
    }

    [Theory]
    [InlineData("0770123456")]
    [InlineData("056123456")]
    public void Client_MobileAlgerien_Valide_Passe(string mobile)
    {
        var validator = new CreateClientRequestValidator();
        var request = ValidClient();
        request.Mobile = mobile;

        Assert.True(validator.Validate(request).IsValid);
    }

    [Theory]
    [InlineData("07712345")]
    [InlineData("01234567890")]
    public void Client_MobileInvalide_EstRefuse(string mobile)
    {
        var validator = new CreateClientRequestValidator();
        var request = ValidClient();
        request.Mobile = mobile;

        Assert.False(validator.Validate(request).IsValid);
    }

    // ---------------------------------------------------------------- préférence splash

    [Fact]
    public async Task Splash_Preference_Persistee()
    {
        var root = Path.Combine(Path.GetTempPath(), $"factur-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["App:DataRoot"] = root,
                })
                .Build();

            var service = new SettingsService(configuration);
            var initial = await service.GetAsync();
            Assert.True(initial.SplashEnabled);

            await service.SaveAsync(new AppSettings { SplashEnabled = false }, default);
            Assert.False((await service.GetAsync()).SplashEnabled);

            await service.SaveAsync(new AppSettings { SplashEnabled = true }, default);
            Assert.True((await service.GetAsync()).SplashEnabled);

            Assert.True(File.Exists(Path.Combine(root, "settings.json")));
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                // Nettoyage au mieux.
            }
        }
    }

    // ---------------------------------------------------------------- helpers

    private static CreateClientRequest ValidClient() => new()
    {
        DisplayName = "Client de test",
        Phone = "0550123456",
        Address = "Cité 20 Août 1956, Alger",
    };

    private async Task<Guid> EnsureClientIdAsync()
    {
        var response = await _client.GetAsync("/api/clients");
        var paged = (await response.Content.ReadFromJsonAsync<PagedResultDto<ClientItem>>(Json))!;
        if (paged.Items.Count > 0)
        {
            return paged.Items[0].Id;
        }

        var create = await _client.PostAsJsonAsync("/api/clients", new
        {
            displayName = $"Client Maintenance {Guid.NewGuid():N}"[..24],
            type = "Entreprise",
            nif = "099916000000013",
            phone = "0550123456",
            address = "Cité 20 Août 1956, Alger",
        });
        create.EnsureSuccessStatusCode();
        return await create.Content.ReadFromJsonAsync<Guid>(Json);
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string uri, object? body)
    {
        var request = new HttpRequestMessage(method, uri);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        return _client.SendAsync(request);
    }

    // ---------------------------------------------------------------- DTOs

    private sealed record InvoiceCreated(Guid Id, string InvoiceNumber);

    private sealed record ClientItem(Guid Id, string DisplayName);

    private sealed record PagedResultDto<T>(List<T> Items, int TotalCount, int Page, int PageSize, int TotalPages);

    private sealed record UnpaidItem(Guid Id, string InvoiceNumber, decimal SoldeRestant);
}
