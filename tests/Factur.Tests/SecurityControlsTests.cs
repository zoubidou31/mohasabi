using Factur.Infrastructure.Export;
using Factur.Infrastructure.Services;

namespace Factur.Tests;

/// <summary>
/// Régression guards pour les correctifs de sécurité du rapport d'audit :
/// URL allowlist, injection de formules (Excel/CSV). Ces tests unitaires valident
/// les contrôles déjà implémentés sans toucher la base de données.
/// </summary>
public class SecurityControlsTests
{
    [Theory]
    [InlineData("https://github.com/zoubidou31/mohasabi/releases/latest/download/Mohasabi_setup.exe", true)]
    [InlineData("https://github.com/zoubidou31/mohasabi/releases/download/v1.0.0/Mohasabi_setup.exe", true)]
    [InlineData("https://raw.githubusercontent.com/zoubidou31/mohasabi/main/version.json", true)]
    [InlineData("https://gist.githubusercontent.com/x/y/v", true)]
    [InlineData("https://objects.githubusercontent.com/x/y", true)]
    [InlineData("http://evil.com/setup.exe", false)]                       // hôte non autorisé
    [InlineData("ftp://github.com/setup.exe", false)]                     // schéma non https
    [InlineData("https://github.com.evil.net/x", false)]                   // suffixe piégé
    [InlineData("https://github.io.evil/setup", false)]
    [InlineData("https://gist.github.com/x", true)]                       // *.github.com autorisé
    [InlineData("javascript:alert(1)", false)]
    [InlineData("not-a-url", false)]
    public void IsAllowedUpdateUrl_RestreintAuxHotesGithub(string url, bool expected)
    {
        Assert.Equal(expected, UpdateService.IsAllowedUpdateUrl(url));
    }

    [Theory]
    [InlineData("=CMD()", "'=CMD()")]
    [InlineData("+1+1", "'+1+1")]
    [InlineData("-2+3+4", "'-2+3+4")]
    [InlineData("@SUM(A1:A5)", "'@SUM(A1:A5)")]
    [InlineData("\t=cmd", "'\t=cmd")]
    [InlineData("\r=cmd", "'\r=cmd")]
    [InlineData("  =cmd", "  =cmd")]                       // espace précédent protège
    [InlineData("Société", "Société")]
    [InlineData("100.00", "100.00")]
    [InlineData(null, "")]
    public void FormulaSanitizer_NeutraliseLesFormulesExcel(string? input, string expected)
    {
        Assert.Equal(expected, FormulaSanitizer.Sanitize(input));
    }

    [Fact]
    public void FormulaSanitizer_SanitizeRow_AppliqueLaNeutralisationAToutesLesCellules()
    {
        var row = new object?[] { "=cmd", "normal", 42, null, "@evil" };
        var sanitized = FormulaSanitizer.SanitizeRow(row);
        Assert.Equal("'=cmd", sanitized[0]);
        Assert.Equal("normal", sanitized[1]);
        Assert.Equal(42, sanitized[2]);            // nombres inchangés
        Assert.Equal(string.Empty, sanitized[3]);  // null -> vide
        Assert.Equal("'@evil", sanitized[4]);
    }
}
