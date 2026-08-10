using Factur.Application.DTOs;
using Factur.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Factur.Api.Controllers;

[ApiController]
[Route("api/restore")]
public class RestoreController : ControllerBase
{
    private readonly IRestoreService _restoreService;
    private readonly IAppStatusService _appStatus;

    public RestoreController(IRestoreService restoreService, IAppStatusService appStatus)
    {
        _restoreService = restoreService;
        _appStatus = appStatus;
    }

    /// <summary>Prépare la restauration d'une sauvegarde (appliquée au prochain démarrage).</summary>
    [HttpPost]
    public async Task<ActionResult<RestoreResult>> Restore([FromBody] RestoreRequest? request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.FileName))
        {
            return BadRequest(new { message = "Nom de sauvegarde manquant." });
        }

        var result = await _restoreService.RestoreAsync(request, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        // Le redémarrage prochain est attendu (restauration) : il ne doit pas être
        // interprété comme un arrêt anormal par la détection d'arrêt propre.
        if (result.RequiresRestart)
        {
            _appStatus.SetRestartPending(true);
        }

        return Ok(result);
    }
}
