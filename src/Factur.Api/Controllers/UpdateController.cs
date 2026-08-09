using System.Diagnostics;
using Factur.Application.DTOs;
using Factur.Application.Interfaces;
using Factur.Infrastructure.Services;
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
    private readonly IWebHostEnvironment _environment;

    public UpdateController(IUpdateService updateService, IWebHostEnvironment environment)
    {
        _updateService = updateService;
        _environment = environment;
    }

    /// <summary>Vérifie si une mise à jour est disponible.</summary>
    [HttpGet("check")]
    public async Task<ActionResult<UpdateCheckResult>> Check(CancellationToken ct)
    {
        return Ok(await _updateService.CheckAsync(ct));
    }

    /// <summary>Télécharge et lance l'installation de la mise à jour, puis quitte l'application.</summary>
    [HttpPost("install")]
    public async Task<ActionResult> Install([FromBody] InstallRequest? request, CancellationToken ct)
    {
        // En production, seule l'URL du manifest fait foi : une URL fournie dans la
        // requête est ignorée (protection contre l'installation d'un binaire arbitraire).
        var check = await _updateService.CheckAsync(ct);
        var downloadUrl = _environment.IsProduction()
            ? check.DownloadUrl
            : (string.IsNullOrWhiteSpace(request?.DownloadUrl) ? check.DownloadUrl : request!.DownloadUrl);

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            return BadRequest(new { message = "URL de téléchargement manquante." });
        }

        if (!UpdateService.IsAllowedUpdateUrl(downloadUrl))
        {
            return BadRequest(new { message = "URL de mise à jour refusée : HTTPS GitHub requis." });
        }

        // L'intégrité du fichier téléchargé doit toujours être vérifiée contre le
        // manifest ; sans empreinte, aucune installation n'est possible.
        if (string.IsNullOrWhiteSpace(check.Sha256))
        {
            return BadRequest(new { message = "L'empreinte SHA-256 du manifest est absente : mise à jour refusée." });
        }

        string installerPath;
        try
        {
            installerPath = await _updateService.DownloadInstallerAsync(downloadUrl, check.Sha256, ct);
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

    private static void MarkUpdatePending()
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mohasabi");
            Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(Path.Combine(dir, "update-pending"), DateTime.UtcNow.ToString("O"));
        }
        catch
        {
            // Non bloquant : le launcher retombera sur un redémarrage classique.
        }
    }
}
