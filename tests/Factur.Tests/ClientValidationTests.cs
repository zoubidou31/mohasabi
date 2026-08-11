using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Factur.Tests;

/// <summary>
/// Scénarios de validation des clients (données algériennes réelles,
/// source unique algeriaLocations.json) : wilaya → commune → code postal,
/// téléphone/mobile algériens, NIF par type, e-mail.
/// </summary>
public class ClientValidationTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public ClientValidationTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    // ---------------------------------------------------------------- wilaya / commune / code postal

    [Fact]
    public async Task Wilaya16_CommuneEtCodeValide_Passe()
    {
        // "Sélection de la wilaya 16 → ses communes" et "commune → code postal valide".
        var payload = ValidEntreprise();
        payload.Wilaya = "16";
        payload.City = "Kouba";
        payload.PostalCode = "16006";

        var response = await SendAsync(HttpMethod.Post, "/api/clients", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CodePostalDuneAutreWilaya_Refuse()
    {
        // 17063 appartient à Messaad (wilaya 69), pas à Kouba (wilaya 16).
        var payload = ValidEntreprise();
        payload.Wilaya = "16";
        payload.City = "Kouba";
        payload.PostalCode = "17063";

        var response = await SendAsync(HttpMethod.Post, "/api/clients", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NouvelleWilaya59_CodeParentValide_Passe()
    {
        // Les wilayas 59-69 utilisent encore la baraque de la wilaya parente :
        // Aflou (59) → codes en 03, El Beidha → 03013.
        var payload = ValidEntreprise();
        payload.Wilaya = "59";
        payload.City = "El Beidha";
        payload.PostalCode = "03013";

        var response = await SendAsync(HttpMethod.Post, "/api/clients", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CommuneSansCodesOfficiels_CodeDeFormeValide_Passe()
    {
        // Ouled Zouai (wilaya 04) n'a aucun code officiel dans le jeu de données :
        // un code 5 chiffres de bonne forme et de bon préfixe reste accepté.
        var payload = ValidEntreprise();
        payload.Wilaya = "04";
        payload.City = "Ouled Zouai";
        payload.PostalCode = "04000";

        var response = await SendAsync(HttpMethod.Post, "/api/clients", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ---------------------------------------------------------------- téléphone / mobile

    [Fact]
    public async Task TelephoneOoredoo_Valide_Passe()
    {
        var payload = ValidEntreprise();
        payload.Phone = "0550123456";
        var response = await SendAsync(HttpMethod.Post, "/api/clients", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task TelephoneMobilis_Valide_Passe()
    {
        var payload = ValidEntreprise();
        payload.Phone = "0660123456";
        var response = await SendAsync(HttpMethod.Post, "/api/clients", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task TelephoneDjezzy_Valide_Passe()
    {
        var payload = ValidEntreprise();
        payload.Phone = "0770123456";
        var response = await SendAsync(HttpMethod.Post, "/api/clients", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task TelephoneTropCourt_Refuse()
    {
        var payload = ValidEntreprise();
        payload.Phone = "0551234";
        var response = await SendAsync(HttpMethod.Post, "/api/clients", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TelephoneVide_Passe()
    {
        // Le téléphone fixe est optionnel : vide → enregistrement sans erreur.
        var payload = ValidEntreprise();
        payload.Phone = "";
        var response = await SendAsync(HttpMethod.Post, "/api/clients", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task MobileValide_TelephoneVide_Passe()
    {
        // Mobile algérien valide + téléphone fixe vide → enregistrement sans erreur.
        var payload = ValidEntreprise();
        payload.Phone = "";
        payload.Mobile = "0770123456";
        var response = await SendAsync(HttpMethod.Post, "/api/clients", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task MobileNonAlgerien_Refuse()
    {
        // Le mobile doit respecter les règles de numérotation algériennes.
        var payload = ValidEntreprise();
        payload.Mobile = "060123456789";
        var response = await SendAsync(HttpMethod.Post, "/api/clients", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------------------------------------------------------------- e-mail

    [Fact]
    public async Task EmailValide_Passe()
    {
        var payload = ValidEntreprise();
        payload.Email = "contact@firma.dz";
        var response = await SendAsync(HttpMethod.Post, "/api/clients", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task EmailInvalideOuIndesirable_Refuse()
    {
        // Domaine non autorisé (.xyz) : refusé.
        var payload = ValidEntreprise();
        payload.Email = "contact@firma.xyz";
        var response = await SendAsync(HttpMethod.Post, "/api/clients", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EmailDisposable_Refuse()
    {
        // Domaine jetable (temporaire) : refusé même si la forme est valide.
        var payload = ValidEntreprise();
        payload.Email = "user@mailinator.com";
        var response = await SendAsync(HttpMethod.Post, "/api/clients", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EmailDisposableSousDomaine_Refuse()
    {
        // Sous-domaine d'un domaine jetable : refusé.
        var payload = ValidEntreprise();
        payload.Email = "user@tmp.yopmail.com";
        var response = await SendAsync(HttpMethod.Post, "/api/clients", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EmailVide_Passe()
    {
        // L'e-mail est optionnel.
        var payload = ValidEntreprise();
        payload.Email = "";
        var response = await SendAsync(HttpMethod.Post, "/api/clients", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ---------------------------------------------------------------- NIF / type de client

    [Fact]
    public async Task EntrepriseSansNIF_Refuse()
    {
        var payload = ValidEntreprise();
        payload.NIF = "";
        var response = await SendAsync(HttpMethod.Post, "/api/clients", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EntrepriseSansRC_Accepte()
    {
        // RC et ART restent facultatifs pour une entreprise.
        var payload = ValidEntreprise();
        payload.RC = "";
        payload.ART = "";
        var response = await SendAsync(HttpMethod.Post, "/api/clients", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task ParticulierSansFichiersFiscaux_Accepte()
    {
        // Un particulier n'a pas à fournir NIF/RC/ART.
        var payload = ValidEntreprise();
        payload.Type = "Particulier";
        payload.NIF = "";
        payload.RC = "";
        payload.ART = "";
        payload.Wilaya = "";
        payload.City = "";
        payload.PostalCode = "";
        var response = await SendAsync(HttpMethod.Post, "/api/clients", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ---------------------------------------------------------------- persistance

    [Fact]
    public async Task EntrepriseComplete_EnregistreeEtRetrouvee()
    {
        var payload = ValidEntreprise();
        var create = await SendAsync(HttpMethod.Post, "/api/clients", payload);
        create.EnsureSuccessStatusCode();
        var id = await create.Content.ReadFromJsonAsync<Guid>(Json);

        var get = await SendAsync(HttpMethod.Get, $"/api/clients/{id}", null);
        get.EnsureSuccessStatusCode();
        var client = (await get.Content.ReadFromJsonAsync<ClientDto>(Json))!;

        Assert.Equal("Kouba", client.City);
        Assert.Equal("16", client.Wilaya);
        Assert.Equal("16006", client.PostalCode);
        Assert.Equal("099916000000013", client.NIF);
        Assert.Equal("0550123456", client.Phone);
        Assert.Equal("contact@firma.dz", client.Email);
    }

    // ---------------------------------------------------------------- helpers

    private ClientPayload ValidEntreprise() => new();

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

    private sealed class ClientPayload
    {
        public string DisplayName { get; set; } = $"Client Test {Guid.NewGuid():N}"[..20];
        public string Type { get; set; } = "Entreprise";
        public string NIF { get; set; } = "099916000000013";
        public string RC { get; set; } = "16/00-0000000B00";
        public string ART { get; set; } = "0999160000000";
        public string Address { get; set; } = "Cité 20 Août 1956, Alger";
        public string Wilaya { get; set; } = "16";
        public string City { get; set; } = "Kouba";
        public string PostalCode { get; set; } = "16006";
        public string Phone { get; set; } = "0550123456";
        public string Mobile { get; set; } = "0660123456";
        public string Email { get; set; } = "contact@firma.dz";
        public string DefaultPaymentMethod { get; set; } = "Comptant";
        public string? Notes { get; set; } = "Client de validation";
    }

    private sealed record ClientDto(
        Guid Id,
        string DisplayName,
        string? NIF,
        string? RC,
        string? ART,
        string Address,
        string? PostalCode,
        string? City,
        string? Wilaya,
        string Phone,
        string? Mobile,
        string? Email);
}
