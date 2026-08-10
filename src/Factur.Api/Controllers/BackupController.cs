using Factur.Application.DTOs;
using Factur.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Factur.Api.Controllers;

[ApiController]
[Route("api/backup")]
public class BackupController : ControllerBase
{
    private readonly IBackupService _backupService;

    public BackupController(IBackupService backupService)
    {
        _backupService = backupService;
    }

    /// <summary>État global du système de sauvegarde.</summary>
    [HttpGet("status")]
    public async Task<ActionResult<BackupStatusDto>> Status(CancellationToken ct)
    {
        return Ok(await _backupService.GetStatusAsync(ct));
    }

    /// <summary>Liste des sauvegardes existantes.</summary>
    [HttpGet("list")]
    public async Task<ActionResult<IReadOnlyList<BackupInfo>>> List(CancellationToken ct)
    {
        return Ok(await _backupService.ListAsync(ct));
    }

    /// <summary>Déclenche une sauvegarde immédiate.</summary>
    [HttpPost("now")]
    public async Task<ActionResult<BackupRunResult>> CreateNow(CancellationToken ct)
    {
        var result = await _backupService.CreateAsync(ct);
        if (!result.Success)
        {
            return StatusCode(500, result);
        }
        return Ok(result);
    }

    /// <summary>Supprime une sauvegarde.</summary>
    [HttpDelete("{fileName}")]
    public async Task<ActionResult> Delete(string fileName, CancellationToken ct)
    {
        try
        {
            await _backupService.DeleteAsync(fileName, ct);
            return NoContent();
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { message = "Sauvegarde introuvable." });
        }
        catch (ArgumentException)
        {
            return BadRequest(new { message = "Nom de fichier invalide." });
        }
    }
}
