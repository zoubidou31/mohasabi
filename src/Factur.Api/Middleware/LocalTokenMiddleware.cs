using System.Security.Cryptography;
using System.Text;

namespace Factur.Api.Middleware;

/// <summary>
/// Protège l'API locale : toute requête sous /api doit présenter le jeton éphémère
/// injecté par le launcher (en-tête Authorization : Bearer). Sans jeton configuré
/// (développement, tests), le middleware laisse passer.
/// Seuls les fichiers téléversés (/api/files) sont exemptés : ils sont référencés
/// directement dans des balises &lt;img&gt;, incapables de porter un en-tête HTTP.
/// </summary>
public class LocalTokenMiddleware
{
    private readonly RequestDelegate _next;
    private readonly byte[]? _expected;
    private readonly ILogger<LocalTokenMiddleware> _logger;

    public LocalTokenMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<LocalTokenMiddleware> logger)
    {
        _next = next;
        _logger = logger;

        var token = configuration["API_TOKEN"];
        _expected = string.IsNullOrWhiteSpace(token) ? null : Encoding.UTF8.GetBytes(token);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_expected is null || !IsApiPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var presented = ExtractBearerToken(context.Request.Headers.Authorization.ToString());
        var matches = presented is not null
                      && presented.Length == _expected.Length
                      && CryptographicOperations.FixedTimeEquals(presented, _expected);

        if (!matches)
        {
            _logger.LogWarning("Requête /api rejetée sans jeton valide sur {Path}", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"status":401,"message":"Non autorisé."}""");
            return;
        }

        await _next(context);
    }

    private static bool IsApiPath(PathString path)
        => path.StartsWithSegments("/api") && !path.StartsWithSegments("/api/files");

    private static byte[]? ExtractBearerToken(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return null;
        }

        const string scheme = "Bearer ";
        var value = header;
        if (value.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
        {
            value = value[scheme.Length..];
        }

        return string.IsNullOrWhiteSpace(value) ? null : Encoding.UTF8.GetBytes(value);
    }
}
