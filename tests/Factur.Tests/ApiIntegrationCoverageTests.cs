using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Factur.Tests;

/// <summary>
/// Tests d'intégration ciblant les chemins de code non encore couverts afin
/// d'atteindre l'objectif de couverture ≥ 80 % (services, exports, rapports).
/// Chaque test crée ses propres données : l'ordre d'exécution n'a pas d'importance.
/// </summary>
public class ApiIntegrationCoverageTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public ApiIntegrationCoverageTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    // ================================================================ Factures

    [Fact]
    public async Task Facture_ModificationBrouillon_PersisteLesChangements()
    {
        var clientId = (await GetOrCreateClientIdAsync());
        var invoice = await CreateInvoiceAsync(clientId, amount: 100m, finalize: false);

        var updated = await SendAsync(HttpMethod.Put, $"/api/invoices/{invoice.Id}", new
        {
            clientId,
            invoiceType = "Facture",
            paymentMethod = "Cheque",
            invoiceDate = DateTime.UtcNow.AddDays(-5),
            validityDays = 15,
            notes = "Note modifiée",
            orderReference = "BC-2026-001",
            lines = new[]
            {
                new { description = "Nouvelle ligne", quantity = 3m, unitPriceHT = 200m, tvaRate = "Normal" },
            },
        });
        updated.EnsureSuccessStatusCode();

        var result = (await updated.Content.ReadFromJsonAsync<InvoiceDto>(Json))!;
        Assert.Equal("Brouillon", result.Status);
        Assert.Equal("Note modifiée", result.Notes);
        Assert.Equal(600m, result.TotalHT);
        Assert.Equal(114m, result.TotalTVA);
        Assert.Equal(714m, result.TotalTTC);
        Assert.Single(result.Lines);
    }

    [Fact]
    public async Task Facture_ModificationNonBrouillon_EstRefusee()
    {
        var clientId = (await GetOrCreateClientIdAsync());
        var invoice = await CreateInvoiceAsync(clientId, amount: 100m, finalize: true);

        var response = await SendAsync(HttpMethod.Put, $"/api/invoices/{invoice.Id}", new
        {
            clientId,
            invoiceType = "Facture",
            lines = new[]
            {
                new { description = "Ligne", quantity = 1m, unitPriceHT = 50m, tvaRate = "Normal" },
            },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Facture_Suppression_Retourne204()
    {
        var clientId = (await GetOrCreateClientIdAsync());
        var invoice = await CreateInvoiceAsync(clientId, amount: 100m, finalize: false);

        var deleted = await SendAsync(HttpMethod.Delete, $"/api/invoices/{invoice.Id}", null);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var missing = await SendAsync(HttpMethod.Get, $"/api/invoices/{invoice.Id}", null);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Facture_PaiementPartiel_PuisSuppressionDePaiement()
    {
        var clientId = (await GetOrCreateClientIdAsync());
        var invoice = await CreateInvoiceAsync(clientId, amount: 1000m, finalize: true);
        Assert.Equal(1190m, invoice.TotalTTC);

        var add = await SendAsync(HttpMethod.Post, $"/api/invoices/{invoice.Id}/payments", new
        {
            paymentDate = DateTime.UtcNow,
            amount = 500m,
            paymentMethod = "Cheque",
            chequeNumber = "CHQ-001",
        });
        Assert.Equal(HttpStatusCode.NoContent, add.StatusCode);

        var after = await GetInvoiceAsync(invoice.Id);
        Assert.Equal("Finalisee", after.Status);
        Assert.Equal(500m, after.MontantPaye);
        Assert.Equal(690m, after.SoldeRestant);
        var paymentId = Assert.Single(after.Payments).Id;

        var remove = await SendAsync(HttpMethod.Delete, $"/api/invoices/{invoice.Id}/payments/{paymentId}", null);
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);

        var restored = await GetInvoiceAsync(invoice.Id);
        Assert.Equal(0m, restored.MontantPaye);
        Assert.Equal(1190m, restored.SoldeRestant);
    }

    [Fact]
    public async Task Facture_PaiementMontantInvalide_EstRefuse()
    {
        var clientId = (await GetOrCreateClientIdAsync());
        var invoice = await CreateInvoiceAsync(clientId, amount: 100m, finalize: true);

        var nul = await SendAsync(HttpMethod.Post, $"/api/invoices/{invoice.Id}/pay", new { amount = 0m });
        Assert.Equal(HttpStatusCode.BadRequest, nul.StatusCode);

        var negatif = await SendAsync(HttpMethod.Post, $"/api/invoices/{invoice.Id}/payments", new { amount = -5m });
        Assert.Equal(HttpStatusCode.BadRequest, negatif.StatusCode);
    }

    [Fact]
    public async Task Facture_Annulation_EtRefusPaiementSurAnnulee()
    {
        var clientId = (await GetOrCreateClientIdAsync());
        var invoice = await CreateInvoiceAsync(clientId, amount: 100m, finalize: false);

        var cancel = await SendAsync(HttpMethod.Post, $"/api/invoices/{invoice.Id}/cancel?reason=Perte de dossier", null);
        cancel.EnsureSuccessStatusCode();
        var cancelled = (await cancel.Content.ReadFromJsonAsync<InvoiceDto>(Json))!;
        Assert.Equal("Annulee", cancelled.Status);

        var doubleCancel = await SendAsync(HttpMethod.Post, $"/api/invoices/{invoice.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.BadRequest, doubleCancel.StatusCode);

        var pay = await SendAsync(HttpMethod.Post, $"/api/invoices/{invoice.Id}/pay", new { amount = (decimal?)null });
        Assert.Equal(HttpStatusCode.BadRequest, pay.StatusCode);

        var payment = await SendAsync(HttpMethod.Post, $"/api/invoices/{invoice.Id}/payments", new { amount = 50m });
        Assert.Equal(HttpStatusCode.BadRequest, payment.StatusCode);
    }

    [Fact]
    public async Task Facture_Duplication_CreeUnBrouillonIdentique()
    {
        var clientId = (await GetOrCreateClientIdAsync());
        var invoice = await CreateInvoiceAsync(clientId, amount: 250m, finalize: true);

        var duplicated = await SendAsync(HttpMethod.Post, $"/api/invoices/{invoice.Id}/duplicate", null);
        duplicated.EnsureSuccessStatusCode();

        var copy = (await duplicated.Content.ReadFromJsonAsync<InvoiceDto>(Json))!;
        Assert.NotEqual(invoice.Id, copy.Id);
        Assert.Equal("Brouillon", copy.Status);
        Assert.Equal(invoice.TotalTTC, copy.TotalTTC);
        Assert.Equal(invoice.Lines.Count, copy.Lines.Count);
        Assert.Equal(clientId, copy.ClientId);
    }

    [Fact]
    public async Task Facture_ImportLignes_AjouteLesLignesValides()
    {
        var clientId = (await GetOrCreateClientIdAsync());
        var invoice = await CreateInvoiceAsync(clientId, amount: 100m, finalize: false);

        // Ligne PROD-001 rattachée au produit existant ; quantité 0 ignorée ; ligne explicite.
        var import = await SendAsync(HttpMethod.Post, $"/api/invoices/{invoice.Id}/import-lines", new object[]
        {
            new { reference = "PROD-001", quantity = 2m, unitPriceHT = 100m },
            new { reference = "REF-IGNOREE", quantity = 0m, unitPriceHT = 100m, tvaRate = "Normal" },
            new { reference = "REF-VALIDE", quantity = 1m, unitPriceHT = 50m, tvaRate = "Reduit" },
        });
        import.EnsureSuccessStatusCode();
        var added = (await import.Content.ReadFromJsonAsync<AddedDto>(Json))!.Added;
        Assert.Equal(2, added);

        var after = await GetInvoiceAsync(invoice.Id);
        Assert.Equal(3, after.Lines.Count);

        var empty = await SendAsync(HttpMethod.Post, $"/api/invoices/{invoice.Id}/import-lines", Array.Empty<object>());
        empty.EnsureSuccessStatusCode();
        Assert.Equal(0, (await empty.Content.ReadFromJsonAsync<AddedDto>(Json))!.Added);
    }

    [Fact]
    public async Task Factures_Liste_FiltresTriPagination()
    {
        var clientId = (await GetOrCreateClientIdAsync());

        var a = await CreateInvoiceAsync(clientId, amount: 100m, finalize: true);
        await CreateInvoiceAsync(clientId, amount: 500m, finalize: false);

        var all = await GetInvoicesAsync("");
        Assert.True(all.TotalCount >= 2);

        var finalisees = await GetInvoicesAsync("?status=Finalisee");
        Assert.True(finalisees.TotalCount >= 1);
        Assert.All(finalisees.Items, i => Assert.Equal("Finalisee", i.Status));

        var aDetail = await GetInvoiceAsync(a.Id);
        var tail = aDetail.InvoiceNumber[^4..];
        var search = await GetInvoicesAsync($"?search={Uri.EscapeDataString(tail)}");
        Assert.Contains(search.Items, i => i.Id == a.Id);

        var amount = await GetInvoicesAsync($"?clientId={clientId}&minAmount=200&maxAmount=600&page=1&pageSize=5");
        Assert.True(amount.TotalCount >= 1);
        Assert.All(amount.Items, i => Assert.InRange(i.TotalTTC, 200m, 600m));

        foreach (var sortBy in new[] { "number", "date", "client", "total", "status", "inconnu" })
        {
            var sorted = await GetInvoicesAsync($"?sortBy={sortBy}&sortDescending=false");
            Assert.True(sorted.TotalCount >= 1);
        }

        var overdue = await GetInvoicesAsync("?overdue=true");
        Assert.NotNull(overdue);
    }

    [Fact]
    public async Task Facture_ProchainNumero_RespecteLeFormat()
    {
        var clientId = (await GetOrCreateClientIdAsync());
        var companyResponse = await SendAsync(HttpMethod.Get, "/api/company", null);
        var company = (await companyResponse.Content.ReadFromJsonAsync<CompanyDto>(Json))!;

        var first = await SendAsync(HttpMethod.Get, "/api/invoices/next-number?date=2026-01-15", null);
        var number1 = (await first.Content.ReadFromJsonAsync<NumberDto>(Json))!.Number;
        Assert.Equal($"{company.InvoicePrefix}-2026-01-000001", number1);

        await CreateInvoiceAsync(clientId, amount: 100m, finalize: true, date: new DateTime(2026, 1, 10));

        var second = await SendAsync(HttpMethod.Get, "/api/invoices/next-number?date=2026-01-15", null);
        Assert.Equal($"{company.InvoicePrefix}-2026-01-000002", (await second.Content.ReadFromJsonAsync<NumberDto>(Json))!.Number);
    }

    [Fact]
    public async Task Facture_Inconnue_Retourne404()
    {
        var response = await SendAsync(HttpMethod.Get, $"/api/invoices/{Guid.NewGuid()}", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ================================================================ Clients

    [Fact]
    public async Task Client_MiseAJour_EtConsultation()
    {
        var id = await CreateClientAsync("Client MAJ");

        var update = await SendAsync(HttpMethod.Put, $"/api/clients/{id}", new
        {
            displayName = "Client MAJ Renommé",
            type = "Entreprise",
            nif = "099916000000013",
            address = "Alger Centre",
        });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var get = await SendAsync(HttpMethod.Get, $"/api/clients/{id}", null);
        get.EnsureSuccessStatusCode();
        var client = (await get.Content.ReadFromJsonAsync<ClientDto>(Json))!;
        Assert.Equal("Client MAJ Renommé", client.DisplayName);
    }

    [Fact]
    public async Task Client_Statistiques_Calculees()
    {
        var clientId = await CreateClientAsync("Client Stats");

        var invoice = await CreateInvoiceAsync(clientId, amount: 1000m, finalize: true);
        var pay = await SendAsync(HttpMethod.Post, $"/api/invoices/{invoice.Id}/pay", new { amount = (decimal?)null });
        pay.EnsureSuccessStatusCode();

        var stats = await SendAsync(HttpMethod.Get, $"/api/clients/{clientId}/stats", null);
        stats.EnsureSuccessStatusCode();
        var result = (await stats.Content.ReadFromJsonAsync<ClientStatsDto>(Json))!;
        Assert.Equal(1, result.InvoiceCount);
        Assert.Equal(1190m, result.TotalSpent);
        Assert.Equal(1190m, result.TotalPaid);
        Assert.Equal(0m, result.Outstanding);
        Assert.Single(result.RecentInvoices);
    }

    [Fact]
    public async Task Client_Import_ImporteUniquementLesValides()
    {
        var name = $"Nouveau Import {Guid.NewGuid():N}"[..20];

        var import = await SendAsync(HttpMethod.Post, "/api/clients/import", new object[]
        {
            new { displayName = name, type = "Entreprise", nif = "099916000000013" },
            new { displayName = name, type = "Entreprise", nif = "099916000000013" },
            new { displayName = "   ", type = "Entreprise" },
        });
        import.EnsureSuccessStatusCode();
        var imported = (await import.Content.ReadFromJsonAsync<ImportedDto>(Json))!.Imported;
        Assert.Equal(1, imported);
    }

    [Fact]
    public async Task Client_AvecFactures_NePeutEtreSupprime()
    {
        var clientId = await CreateClientAsync("Client Bloque");
        await CreateInvoiceAsync(clientId, amount: 100m, finalize: false);

        var deleted = await SendAsync(HttpMethod.Delete, $"/api/clients/{clientId}", null);
        Assert.Equal(HttpStatusCode.Conflict, deleted.StatusCode);
        var body = await deleted.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(
            "Impossible de supprimer ce client car il possède des factures ou documents associés.",
            body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Client_SansFactures_EstSupprime()
    {
        var clientId = await CreateClientAsync("Client Libre");

        var deleted = await SendAsync(HttpMethod.Delete, $"/api/clients/{clientId}", null);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task Client_IdInvalide_EstRejette()
    {
        var deleted = await SendAsync(HttpMethod.Delete, "/api/clients/not-a-valid-uuid", null);
        Assert.Equal(HttpStatusCode.BadRequest, deleted.StatusCode);
    }

    [Fact]
    public async Task Client_Archivage_Succes()
    {
        var clientId = await CreateClientAsync("Client A archiver");

        var archive = await SendAsync(HttpMethod.Patch, $"/api/clients/{clientId}/archive", null);
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        // Masqué de la liste active par défaut...
        var active = await SendAsync(HttpMethod.Get, "/api/clients", null);
        active.EnsureSuccessStatusCode();
        var actifs = (await active.Content.ReadFromJsonAsync<PagedResultDto<ClientDto>>(Json))!.Items;
        Assert.DoesNotContain(actifs, c => c.Id == clientId);

        // ...mais présent dans "Archivés" et dans "Tous".
        var archived = await SendAsync(HttpMethod.Get, "/api/clients?status=archived", null);
        archived.EnsureSuccessStatusCode();
        Assert.Contains((await archived.Content.ReadFromJsonAsync<PagedResultDto<ClientDto>>(Json))!.Items, c => c.Id == clientId);

        var all = await SendAsync(HttpMethod.Get, "/api/clients?status=all", null);
        all.EnsureSuccessStatusCode();
        Assert.Contains((await all.Content.ReadFromJsonAsync<PagedResultDto<ClientDto>>(Json))!.Items, c => c.Id == clientId);
    }

    [Fact]
    public async Task Client_ArchiveAvecFactures_GardeLesDocuments()
    {
        var clientId = await CreateClientAsync("Client Avec Factures Archive");
        await CreateInvoiceAsync(clientId, amount: 100m, finalize: true);

        // L'archivage est autorisé même s'il possède des factures.
        var archive = await SendAsync(HttpMethod.Patch, $"/api/clients/{clientId}/archive", null);
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        // Les factures et documents comptables restent intacts.
        var invoices = await SendAsync(HttpMethod.Get, $"/api/invoices?clientId={clientId}", null);
        invoices.EnsureSuccessStatusCode();
        var page = (await invoices.Content.ReadFromJsonAsync<Factur.Application.DTOs.PagedResult<Factur.Application.DTOs.InvoiceSummaryDto>>(Json))!;
        Assert.NotEmpty(page!.Items);

        // Le client archivé est bien masqué de la liste active.
        var active = await SendAsync(HttpMethod.Get, "/api/clients", null);
        active.EnsureSuccessStatusCode();
        var actifs = (await active.Content.ReadFromJsonAsync<PagedResultDto<ClientDto>>(Json))!.Items;
        Assert.DoesNotContain(actifs, c => c.Id == clientId);
    }

    // ================================================================ Produits

    [Fact]
    public async Task Produit_CycleDeVieComplet()
    {
        var id = await CreateProductAsync("REF-TEST-1", "Produit test", "CatA", isActive: true);

        var doublon = await SendAsync(HttpMethod.Post, "/api/products", new
        {
            reference = "REF-TEST-1",
            name = "Doublon",
            defaultPrice = 10m,
            defaultTvaRate = "Normal",
        });
        Assert.Equal(HttpStatusCode.BadRequest, doublon.StatusCode);

        var get = await SendAsync(HttpMethod.Get, $"/api/products/{id}", null);
        get.EnsureSuccessStatusCode();
        var product = (await get.Content.ReadFromJsonAsync<ProductDto>(Json))!;
        Assert.Equal("Produit test", product.Name);

        var search = await SendAsync(HttpMethod.Get, "/api/products?search=test", null);
        Assert.Contains((await search.Content.ReadFromJsonAsync<PagedResultDto<ProductDto>>(Json))!.Items, p => p.Id == id);

        var category = await SendAsync(HttpMethod.Post, "/api/categories", new { name = "Informatique & Technologie" });
        category.EnsureSuccessStatusCode();

        var categories = await SendAsync(HttpMethod.Get, "/api/products/categories", null);
        Assert.Contains("Informatique & Technologie", (await categories.Content.ReadFromJsonAsync<List<string>>(Json))!);

        var update = await SendAsync(HttpMethod.Put, $"/api/products/{id}", new
        {
            reference = "REF-TEST-1",
            name = "Produit MAJ",
            defaultPrice = 120m,
            defaultTvaRate = "Reduit",
            isActive = false,
        });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var inactif = (await (await SendAsync(HttpMethod.Get, $"/api/products/{id}", null))
            .Content.ReadFromJsonAsync<ProductDto>(Json))!;
        Assert.False(inactif.IsActive);
        Assert.Equal(120m, inactif.DefaultPrice);

        var actifs = await SendAsync(HttpMethod.Get, "/api/products", null);
        Assert.DoesNotContain((await actifs.Content.ReadFromJsonAsync<PagedResultDto<ProductDto>>(Json))!.Items, p => p.Id == id);

        var tous = await SendAsync(HttpMethod.Get, "/api/products?includeInactive=true", null);
        Assert.Contains((await tous.Content.ReadFromJsonAsync<PagedResultDto<ProductDto>>(Json))!.Items, p => p.Id == id);

        var deleted = await SendAsync(HttpMethod.Delete, $"/api/products/{id}", null);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task Produit_Import_ImporteUniquementLesValides()
    {
        var ref1 = $"REF-IMP-{Guid.NewGuid():N}"[..12];

        var import = await SendAsync(HttpMethod.Post, "/api/products/import", new object[]
        {
            new { reference = ref1, name = "Produit importé 1", defaultPrice = 10m, defaultTvaRate = "Normal" },
            new { reference = ref1, name = "Doublon", defaultPrice = 10m, defaultTvaRate = "Normal" },
            new { reference = "   ", name = "Sans référence", defaultPrice = 10m, defaultTvaRate = "Normal" },
        });
        import.EnsureSuccessStatusCode();
        var imported = (await import.Content.ReadFromJsonAsync<ImportedDto>(Json))!.Imported;
        Assert.Equal(1, imported);
    }

    // ================================================================ Rapports

    [Fact]
    public async Task RapportMensuel_AgregeLesFactures()
    {
        var clientId = (await GetOrCreateClientIdAsync());
        await CreateInvoiceAsync(clientId, amount: 300m, finalize: true);

        var now = DateTime.UtcNow;
        var response = await SendAsync(HttpMethod.Get,
            $"/api/reports/monthly?year={now.Year}&month={now.Month}", null);
        response.EnsureSuccessStatusCode();
        var report = (await response.Content.ReadFromJsonAsync<MonthlyReportDto>(Json))!;
        Assert.True(report.InvoiceCount >= 1);
        Assert.True(report.TotalTTC > 0m);
        Assert.NotEmpty(report.TVAByRate);
    }

    [Fact]
    public async Task RapportTVA_TotauxSurPeriode()
    {
        var clientId = (await GetOrCreateClientIdAsync());
        await CreateInvoiceAsync(clientId, amount: 200m, finalize: true);

        var response = await SendAsync(HttpMethod.Get, "/api/reports/tva?from=2020-01-01&to=2030-01-01", null);
        response.EnsureSuccessStatusCode();
        var report = (await response.Content.ReadFromJsonAsync<TVAReportDto>(Json))!;
        Assert.Equal("Total", report.TVARate);
        Assert.True(report.TVAAmount > 0m);
        Assert.True(report.TotalTTC > 0m);
    }

    [Fact]
    public async Task RapportImpayes_ListeLesFacturesEchues()
    {
        var clientId = (await GetOrCreateClientIdAsync());
        var invoice = await CreateInvoiceAsync(clientId, amount: 150m, finalize: true,
            date: DateTime.UtcNow.AddDays(-30), validityDays: 1);

        var response = await SendAsync(HttpMethod.Get, "/api/reports/unpaid", null);
        response.EnsureSuccessStatusCode();
        var unpaid = (await response.Content.ReadFromJsonAsync<List<InvoiceSummaryDto>>(Json))!;
        Assert.Contains(unpaid, i => i.Id == invoice.Id);
    }

    // ================================================================ Société / Fichiers

    [Fact]
    public async Task Societe_Modification_Et_LogoServi()
    {
        var save = await SendAsync(HttpMethod.Put, "/api/company", new
        {
            companyName = "Ma Société Test",
            address = "Cité 123, Alger, Dar El Beïda",
            city = "Alger",
            wilaya = "16",
            postalCode = "16000",
            phone = "0550112233",
            mobile = "0660112233",
            email = "test@masociete.dz",
            nif = "099916000000000",
            nis = "099916000000000",
            rc = "16/00-0000000B00",
            art = "0000000000000",
            rib = "00799999000012345678",
            ccp = "1234567",
            bankName = "BNA",
            invoicePrefix = "MAF",
            invoiceSerie = "A",
            validityDays = 15,
            defaultTvaRate = "Normal",
            paymentConditions = "Paiement comptant.",
            penalties = "1% par mois de retard.",
            bankAccountNumber = "00799999000012345678",
            useBankersRounding = false,
            logoData = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==",
        });
        save.EnsureSuccessStatusCode();
        var company = (await save.Content.ReadFromJsonAsync<CompanyDto>(Json))!;
        Assert.Equal("Ma Société Test", company.CompanyName);
        Assert.Equal("MAF", company.InvoicePrefix);
        Assert.False(string.IsNullOrWhiteSpace(company.LogoPath));

        var fileName = company.LogoPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)[^1];
        var file = await _client.GetAsync($"/api/files/{fileName}");
        file.EnsureSuccessStatusCode();
        Assert.Equal("image/png", file.Content.Headers.ContentType!.MediaType);
        Assert.True((await file.Content.ReadAsByteArrayAsync()).Length > 0);
    }

    [Fact]
    public async Task Fichier_Inconnu_Retourne404()
    {
        var response = await _client.GetAsync("/api/files/inexistant.png");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ================================================================ Exports

    [Fact]
    public async Task ExportExcel_RetourneUnXlsx()
    {
        var clientId = (await GetOrCreateClientIdAsync());
        var invoice = await CreateInvoiceAsync(clientId, amount: 100m, finalize: true);

        var response = await SendAsync(HttpMethod.Get, $"/api/invoices/{invoice.Id}/export/xlsx", null);
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType!.MediaType);
        Assert.True((await response.Content.ReadAsByteArrayAsync()).Length > 1000);
    }

    [Fact]
    public async Task ExportWord_RetourneUnDocx()
    {
        var clientId = (await GetOrCreateClientIdAsync());
        var invoice = await CreateInvoiceAsync(clientId, amount: 100m, finalize: true);

        var response = await SendAsync(HttpMethod.Get, $"/api/invoices/{invoice.Id}/export/docx", null);
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", response.Content.Headers.ContentType!.MediaType);
        Assert.True((await response.Content.ReadAsByteArrayAsync()).Length > 1000);
    }

    [Fact]
    public async Task ExportListeExcel_RetourneUnXlsx()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/invoices/export/xlsx", null);
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType!.MediaType);
        Assert.True((await response.Content.ReadAsByteArrayAsync()).Length > 1000);
    }

    // ================================================================ E-mail / Audit

    [Fact]
    public async Task EnvoiEmail_SansSMTP_Retourne400()
    {
        var clientId = (await GetOrCreateClientIdAsync());
        var invoice = await CreateInvoiceAsync(clientId, amount: 100m, finalize: true);

        var sansDestinataire = await SendAsync(HttpMethod.Post, $"/api/invoices/{invoice.Id}/send-email", null);
        Assert.Equal(HttpStatusCode.BadRequest, sansDestinataire.StatusCode);

        var sansSmtp = await SendAsync(HttpMethod.Post, $"/api/invoices/{invoice.Id}/send-email?to=client@exemple.dz", null);
        Assert.Equal(HttpStatusCode.BadRequest, sansSmtp.StatusCode);
    }

    [Fact]
    public async Task Audit_ListeEtFiltres()
    {
        var clientId = (await GetOrCreateClientIdAsync());
        await CreateInvoiceAsync(clientId, amount: 100m, finalize: true);

        var all = await SendAsync(HttpMethod.Get, "/api/audit", null);
        all.EnsureSuccessStatusCode();
        var logs = (await all.Content.ReadFromJsonAsync<List<AuditLogDto>>(Json))!;
        Assert.NotEmpty(logs);

        var invoices = await SendAsync(HttpMethod.Get, "/api/audit?entityType=Invoice", null);
        Assert.NotEmpty((await invoices.Content.ReadFromJsonAsync<List<AuditLogDto>>(Json))!);

        var limited = await SendAsync(HttpMethod.Get, "/api/audit?limit=2", null);
        Assert.True((await limited.Content.ReadFromJsonAsync<List<AuditLogDto>>(Json))!.Count <= 2);

        var period = await SendAsync(HttpMethod.Get, "/api/audit?from=2020-01-01&to=2030-01-01", null);
        Assert.Equal(HttpStatusCode.OK, period.StatusCode);
    }

    // ================================================================ Helpers

    private async Task<List<ClientDto>> GetClientsAsync()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/clients", null);
        response.EnsureSuccessStatusCode();
        var paged = (await response.Content.ReadFromJsonAsync<PagedResultDto<ClientDto>>(Json))!;
        return paged.Items;
    }

    private async Task<Guid> GetOrCreateClientIdAsync()
    {
        var clients = await GetClientsAsync();
        if (clients.Count > 0)
        {
            return clients[0].Id;
        }

        return await CreateClientAsync("Client Test");
    }

    private async Task<PagedDto> GetInvoicesAsync(string query)
    {
        var response = await SendAsync(HttpMethod.Get, "/api/invoices" + query, null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PagedDto>(Json))!;
    }

    private async Task<InvoiceDto> GetInvoiceAsync(Guid id)
    {
        var response = await SendAsync(HttpMethod.Get, $"/api/invoices/{id}", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InvoiceDto>(Json))!;
    }

    private async Task<Guid> CreateClientAsync(string displayName)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/clients", new
        {
            displayName = $"{displayName} {Guid.NewGuid():N}"[..20],
            type = "Entreprise",
            nif = "099916000000013",
        });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(Json);
    }

    private async Task<Guid> CreateProductAsync(string reference, string name, string? category, bool isActive)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/products", new
        {
            reference,
            name,
            category,
            defaultPrice = 100m,
            defaultTvaRate = "Normal",
            isActive,
        });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(Json);
    }

    private async Task<InvoiceDto> CreateInvoiceAsync(
        Guid clientId, decimal amount,
        bool finalize = false, DateTime? date = null, int validityDays = 30)
    {
        var create = await SendAsync(HttpMethod.Post, "/api/invoices", new
        {
            clientId,
            invoiceType = "Facture",
            paymentMethod = "Comptant",
            invoiceDate = date ?? DateTime.UtcNow,
            validityDays,
            lines = new[]
            {
                new { description = "Prestation", quantity = 1m, unitPriceHT = amount, tvaRate = "Normal" },
            },
        });
        create.EnsureSuccessStatusCode();
        var invoice = (await create.Content.ReadFromJsonAsync<InvoiceDto>(Json))!;

        if (finalize)
        {
            var fin = await SendAsync(HttpMethod.Post, $"/api/invoices/{invoice.Id}/finalize", null);
            fin.EnsureSuccessStatusCode();
            invoice = (await fin.Content.ReadFromJsonAsync<InvoiceDto>(Json))!;
        }

        return invoice;
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

    // ================================================================ DTOs

    private sealed record ClientDto(Guid Id, string DisplayName);

    private sealed record ClientStatsDto(Guid ClientId, int InvoiceCount, decimal TotalSpent, decimal TotalPaid,
        decimal Outstanding, DateTime? LastInvoiceDate, List<InvoiceSummaryDto> RecentInvoices);

    private sealed record CompanyDto(string CompanyName, string InvoicePrefix, string? LogoPath);

    private sealed record PagedDto(List<InvoiceSummaryDto> Items, int TotalCount, int Page, int PageSize);

    private sealed record PagedResultDto<T>(List<T> Items, int TotalCount, int Page, int PageSize);

    private sealed record InvoiceSummaryDto(Guid Id, string InvoiceNumber, string ClientName, DateTime InvoiceDate,
        DateTime? DueDate, string InvoiceType, string Status, decimal TotalHT, decimal TotalTVA, decimal TotalTTC,
        decimal MontantPaye, decimal SoldeRestant, bool IsOverdue);

    private sealed record InvoiceDto(Guid Id, string InvoiceNumber, string Status, string InvoiceType,
        decimal TotalHT, decimal TotalTVA, decimal TotalTTC, decimal MontantPaye, decimal SoldeRestant,
        Guid ClientId, string? Notes, List<InvoiceLineDto> Lines, List<PaymentDto> Payments);

    private sealed record InvoiceLineDto(Guid? Id, string Reference, string Description);

    private sealed record PaymentDto(Guid Id, DateTime PaymentDate, decimal Amount);

    private sealed record NumberDto(string Number);

    private sealed record AddedDto(int Added);

    private sealed record ImportedDto(int Imported);

    private sealed record ProductDto(Guid Id, string Reference, string Name, string? Category,
        decimal DefaultPrice, string DefaultTVARate, bool IsActive);

    private sealed record MonthlyReportDto(int Year, int Month, int InvoiceCount, decimal TotalHT,
        decimal TotalTVA, decimal TotalTTC, decimal TotalCollected, decimal Outstanding, List<TVAReportDto> TVAByRate);

    private sealed record TVAReportDto(string TVARate, decimal TotalHT, decimal TVAAmount, decimal TotalTTC);

    private sealed record AuditLogDto(Guid Id, string? UserName, string? EntityType, string? EntityId,
        string? Action, string? ChangedData, DateTime Timestamp);
}
