namespace Factur.Infrastructure.Export;

/// <summary>
/// Protège les exports (Excel, CSV) contre l'injection de formules.
/// Toute valeur texte commençant par = + - @ \t \r est préfixée d'une apostrophe
/// pour être traitée comme du texte pur par les tableurs.
/// </summary>
public static class FormulaSanitizer
{
    private static readonly char[] FormulaPrefixes = ['=', '+', '-', '@', '\t', '\r'];

    /// <summary>Neutralise une valeur chaîne pour un export Excel/CSV.</summary>
    public static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var trimmed = value.TrimStart();
        if (trimmed.Length > 0 && FormulaPrefixes.Contains(trimmed[0]))
        {
            return "'" + value;
        }

        return value;
    }

    /// <summary>Neutralise une valeur objet (string, DateTime, decimal, etc.) pour un export.</summary>
    public static object? Sanitize(object? value)
    {
        if (value is string s)
        {
            return Sanitize(s);
        }
        return value;
    }

    /// <summary>Applique la neutralisation à une liste de valeurs (ligne d'export).</summary>
    public static IReadOnlyList<object?> SanitizeRow(IReadOnlyList<object?> row)
    {
        var sanitized = new object?[row.Count];
        for (var i = 0; i < row.Count; i++)
        {
            sanitized[i] = Sanitize(row[i]);
        }
        return sanitized;
    }
}