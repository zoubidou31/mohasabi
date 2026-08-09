using System.Diagnostics;
using Factur.Application.DTOs;
using Factur.Application.Interfaces;
using Factur.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Factur.Api.Controllers;

public class InstallRequest
{
    public string? DownloadUrl { get; init; }
}

[ApiController]
[Route("api/update")]
public class UpdateController : ControllerBase
{
    private readonly IUpdateService _updateService;

    public UpdateController(IUpdateService updateService)
    {
        _updateService = updateService;
    }

    /// <summary>Vérifie si une mise à jour est disponible.</summary>
    [HttpGet("check")]
    [AllowAnonymous]
    public async Task<ActionResult<UpdateCheckResult>> Check(CancellationToken ct)
    {
        return Ok(await _updateService.CheckAsync(ct));
    }

    /// <summary>Télécharge et lance l'installation de la mise à jour, puis quitte l'application.</summary>
    [HttpPost("install")]
    [AllowAnonymous]
    public async Task<ActionResult> Install([FromBody] InstallRequest? request, CancellationToken ct)
    {
        var check = await _updateService.CheckAsync(ct);
        var fromManifest = string.IsNullOrWhiteSpace(request?.DownloadUrl);
        var downloadUrl = fromManifest ? check.DownloadUrl : request!.DownloadUrl;

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            return BadRequest(new { message = "URL de téléchargement manquante." });
        }

        // En production, seule l'URL du manifest fait foi. Une URL fournie par la
        // requête n'est tolérée que vers un hôte local (tests) car aucune
        // vérification d'intégrité ne peut alors être garantie.
        if (!UpdateService.IsAllowedUpdateUrl(downloadUrl) || (!fromManifest && !IsLoopback(downloadUrl)))
        {
            return BadRequest(new { message = "URL de mise à jour refusée : HTTPS requis (hôte local autorisé)." });
        }

        var expectedSha256 = fromManifest ? check.Sha256 : null;

        string installerPath;
        try
        {
            installerPath = await _updateService.DownloadInstallerAsync(downloadUrl, expectedSha256, ct);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Téléchargement impossible : {ex.Message}" });
        }

        MarkUpdatePending();

        var process = Process.Start(new ProcessStartInfo(installerPath)
        {
            UseShellExecute = true,
            Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
        });

        // Quitte l'API après un court délai pour libérer les fichiers verrouillés.
        _ = Task.Run(async () =>
        {
            await Task.Delay(2000);
            Environment.Exit(0);
        });

        // Nettoie l'installateur téléchargé une fois l'installation terminée.
        _ = Task.Run(async () =>
        {
            try
            {
                if (process is not null && !process.HasExited)
                {
                    process.WaitForExit();
                }
            }
            catch
            {
                // Ignoré : nettoyage au mieux.
            }
            try
            {
                System.IO.File.Delete(installerPath);
            }
            catch
            {
                // Ignoré : fichier conservé si verrouillé.
            }
        });

        return Ok(new { message = "Mise à jour téléchargée. L'application va redémarrer automatiquement.", restarting = true });
    }

    private static bool IsLoopback(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.IsLoopback;

    private static void MarkUpdatePending()
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mohasabi");
            Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(Path.Combine(dir, "update-pending"), DateTime.UtcNow.ToString("O"));        }
        catch
        {
            // Non bloquant : le launcher retombera sur un redémarrage classique.
        }
    }
}
