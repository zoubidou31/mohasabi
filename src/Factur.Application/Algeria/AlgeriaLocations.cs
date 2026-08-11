using System.Reflection;
using System.Text.Json;

namespace Factur.Application.Algeria;

/// <summary>Wilaya (découpage administratif, 69 wilayas après la loi 26-06 de 2025/2026).</summary>
public sealed class WilayaInfo
{
    public string Code { get; init; } = string.Empty;
    public string NameFr { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;

    /// <summary>
    /// Deux premiers chiffres réellement utilisés par Algérie Poste pour cette wilaya.
    /// Pour les wilayas 01-58 il s'agit du numéro de wilaya (règle officielle).
    /// Pour les 11 nouvelles wilayas 59-69, Algérie Poste conserve encore la baraque
    /// de la wilaya parente historique (ex. Aflou 59 → 03, Messaad 69 → 17) ; on stocke
    /// donc la baraque réelle, jamais inventée.
    /// </summary>
    public string? PostalPrefix { get; init; }
}

/// <summary>Commune (baladiya) rattachée à une wilaya, avec ses codes postaux officiels Algérie Poste.</summary>
public sealed class CommuneInfo
{
    public string WilayaCode { get; init; } = string.Empty;
    public string NameFr { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string DairaFr { get; init; } = string.Empty;
    public IReadOnlyList<string> PostalCodes { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Chargement et requêtes sur la source unique des localités algériennes
/// (algeriaLocations.json, embarquée dans cet assembly). Aucune donnée n'est
/// inventée ici : tout provient du fichier partagé avec le frontend.
/// </summary>
public static class AlgeriaLocations
{
    private sealed class Root
    {
        public MetadataDto? Metadata { get; set; }
        public List<WilayaDto> Wilayas { get; set; } = new();
        public List<CommuneDto> Communes { get; set; } = new();
    }

    private sealed class MetadataDto
    {
        public string? LastUpdated { get; set; }
        public string? PostalCodeSource { get; set; }
        public string? AdminSource { get; set; }
    }

    private sealed class WilayaDto
    {
        public string? Code { get; set; }
        public string? NameFr { get; set; }
        public string? NameEn { get; set; }
        public string? PostalPrefix { get; set; }
    }

    private sealed class CommuneDto
    {
        public string? WilayaCode { get; set; }
        public string? NameFr { get; set; }
        public string? NameEn { get; set; }
        public string? DairaFr { get; set; }
        public List<string>? PostalCodes { get; set; }
    }

    private static readonly Lazy<Root> _root = new(LoadRoot);

    private static readonly Lazy<IReadOnlyList<WilayaInfo>> _wilayas = new(() =>
        _root.Value.Wilayas.Select(w => new WilayaInfo
        {
            Code = w.Code ?? string.Empty,
            NameFr = w.NameFr ?? string.Empty,
            NameEn = w.NameEn ?? string.Empty,
            PostalPrefix = w.PostalPrefix,
        }).ToList());

    private static readonly Lazy<IReadOnlyList<CommuneInfo>> _communes = new(() =>
        _root.Value.Communes.Select(c => new CommuneInfo
        {
            WilayaCode = c.WilayaCode ?? string.Empty,
            NameFr = c.NameFr ?? string.Empty,
            NameEn = c.NameEn ?? string.Empty,
            DairaFr = c.DairaFr ?? string.Empty,
            PostalCodes = c.PostalCodes ?? new List<string>(),
        }).ToList());

    private static readonly Lazy<IReadOnlyDictionary<string, WilayaInfo>> _byCode =
        new(() => _wilayas.Value.ToDictionary(w => w.Code, StringComparer.OrdinalIgnoreCase));

    private static readonly Lazy<IReadOnlyDictionary<string, List<CommuneInfo>>> _byWilaya =
        new(() => _communes.Value
            .GroupBy(c => c.WilayaCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase));

    private static Root LoadRoot()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("algeriaLocations.json", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Ressource algeriaLocations.json introuvable dans l'assembly Application.");

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Impossible d'ouvrir la ressource algeriaLocations.json.");
        using var reader = new StreamReader(stream);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var root = JsonSerializer.Deserialize<Root>(reader.ReadToEnd(), options)
            ?? throw new InvalidOperationException("algeriaLocations.json invalide.");

        return root;
    }

    /// <summary>Date de dernière mise à jour du jeu de données (métadonnées).</summary>
    public static string? LastUpdated => _root.Value.Metadata?.LastUpdated;

    /// <summary>Toutes les wilayas (01 → 69).</summary>
    public static IReadOnlyList<WilayaInfo> Wilayas => _wilayas.Value;

    /// <summary>Toutes les communes.</summary>
    public static IReadOnlyList<CommuneInfo> Communes => _communes.Value;

    public static WilayaInfo? GetWilaya(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        return _byCode.Value.TryGetValue(code.Trim(), out var w) ? w : null;
    }

    public static bool IsValidWilaya(string? code) => GetWilaya(code) is not null;

    /// <summary>Communes d'une wilaya (liste vide si inconnue).</summary>
    public static IReadOnlyList<CommuneInfo> GetCommunes(string? wilayaCode)
    {
        if (string.IsNullOrWhiteSpace(wilayaCode)) return Array.Empty<CommuneInfo>();
        return _byWilaya.Value.TryGetValue(wilayaCode.Trim(), out var list) ? list : Array.Empty<CommuneInfo>();
    }

    /// <summary>
    /// Recherche d'une commune par nom (fr ou en, insensible à la casse) dans une wilaya.
    /// Retourne null si la commune n'existe pas dans cette wilaya.
    /// </summary>
    public static CommuneInfo? FindCommune(string? wilayaCode, string? communeName)
    {
        if (string.IsNullOrWhiteSpace(wilayaCode) || string.IsNullOrWhiteSpace(communeName)) return null;
        var name = communeName.Trim();
        return GetCommunes(wilayaCode).FirstOrDefault(c =>
            string.Equals(c.NameFr, name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c.NameEn, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Valide un code postal saisi par rapport à la wilaya et à la commune choisies :
    /// - 5 chiffres ;
    /// - quand la wilaya est connue, le code commence par sa baraque officielle ;
    /// - quand la wilaya ET la commune sont connues, le code doit être l'un des codes
    ///   Algérie Poste officiels de la commune (une commune peut en avoir plusieurs,
    ///   ex. Kouba : 16006, 16009, 16055...). Les communes sans code officiel connu
    ///   acceptent tout code à 5 chiffres de bonne forme (on ne rejette pas une adresse
    ///   réelle faute de donnée, mais on ne valide pas non plus un code inexistant).
    /// </summary>
    public static bool IsValidPostalCode(string? wilayaCode, string? communeName, string? postalCode)
    {
        if (string.IsNullOrWhiteSpace(postalCode)) return true;
        var code = postalCode.Trim();
        if (code.Length != 5 || !code.All(char.IsDigit)) return false;

        var wilaya = GetWilaya(wilayaCode);
        if (wilaya is null) return true; // pas de wilaya : forme seule

        var commune = FindCommune(wilayaCode, communeName);
        if (commune is null)
        {
            // Pas de commune : on vérifie seulement la baraque officielle de la wilaya.
            return wilaya.PostalPrefix is null || code.StartsWith(wilaya.PostalPrefix, StringComparison.Ordinal);
        }

        if (commune.PostalCodes.Count == 0)
        {
            // Commune sans code officiel dans le jeu de données : on vérifie la forme.
            return wilaya.PostalPrefix is null || code.StartsWith(wilaya.PostalPrefix, StringComparison.Ordinal);
        }

        return commune.PostalCodes.Contains(code);
    }

    /// <summary>Codes postaux officiels d'une commune (source Algérie Poste).</summary>
    public static IReadOnlyList<string> GetPostalCodes(string? wilayaCode, string? communeName)
    {
        var commune = FindCommune(wilayaCode, communeName);
        return commune?.PostalCodes ?? Array.Empty<string>();
    }
}
