using Factur.Application.DTOs;
using Factur.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Factur.Api.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settingsService;

    public SettingsController(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>Préférences générales actuelles.</summary>
    [HttpGet]
    public async Task<ActionResult<AppSettings>> Get(CancellationToken ct)
    {
        return Ok(await _settingsService.GetAsync(ct));
    }

    /// <summary>Enregistre les préférences générales.</summary>
    [HttpPut]
    public async Task<ActionResult<AppSettings>> Save([FromBody] AppSettings? settings, CancellationToken ct)
    {
        if (settings is null)
        {
            return BadRequest(new { message = "Corps de requête invalide." });
        }
        return Ok(await _settingsService.SaveAsync(settings, ct));
    }
}
