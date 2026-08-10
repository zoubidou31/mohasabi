using System.Diagnostics;
using Factur.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Factur.Api.Controllers;

[ApiController]
[Route("api/app")]
public class AppController : ControllerBase
{
    private readonly IAppStatusService _appStatus;
    private readonly IBackupService _backupService;

    public AppController(IAppStatusService appStatus, IBackupService backupService)
    {
        _appStatus = appStatus;
        _backupService = backupService;
    }

    /// <summary>État de l'application (session précédente interrompue ?).</summary>
    [HttpGet("status")]
    public ActionResult<object> Status()
    {
        return Ok(new { uncleanExit = _appStatus.UncleanExitDetected });
    }

    /// <summary>Ouvre le dossier des sauvegardes dans l'Explorateur Windows.</summary>
    [HttpPost("open-folder")]
    public async Task<ActionResult> OpenBackupFolder(CancellationToken ct)
    {
        var status = await _backupService.GetStatusAsync(ct);
        if (string.IsNullOrWhiteSpace(status.BackupLocation))
        {
            return BadRequest(new { message = "Dossier de sauvegarde inconnu." });
        }

        try
        {
            Directory.CreateDirectory(status.BackupLocation);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{status.BackupLocation}\"") { UseShellExecute = true });
            return Ok(new { opened = true });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    /// <summary>Ferme l'API pour provoquer un redémarrage par le launcher (ex. après restauration).</summary>
    [HttpPost("restart")]
    public ActionResult Restart()
    {
        // Ce redémarrage est toujours initié par l'interface (restauration ou
        // écran d'erreur) : il ne doit pas être signalé comme un arrêt anormal.
        _appStatus.SetRestartPending(true);

        // Le délai laisse le temps à la réponse HTTP d'être délivrée ; le launcher
        // relance alors l'API (aucun marqueur de mise à jour présent).
        _ = Task.Run(async () =>
        {
            await Task.Delay(1500);
            Environment.Exit(0);
        });
        return Ok(new { message = "Redémarrage en cours…", restarting = true });
    }
}
