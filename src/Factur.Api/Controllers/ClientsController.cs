using Factur.Application.Common.Exceptions;
using Factur.Application.DTOs;
using Factur.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Factur.Api.Controllers;

[ApiController]
[Route("api/clients")]
public class ClientsController : ControllerBase
{
    private readonly IClientService _clientService;

    public ClientsController(IClientService clientService)
    {
        _clientService = clientService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ClientDto>>> GetAll([FromQuery] ClientQuery query, CancellationToken ct)
    {
        return Ok(await _clientService.GetPagedAsync(query, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClientDto>> GetById(Guid id, CancellationToken ct)
    {
        return Ok(await _clientService.GetByIdAsync(id, ct));
    }

    [HttpGet("{id:guid}/stats")]
    public async Task<ActionResult<ClientStatsDto>> GetStats(Guid id, CancellationToken ct)
    {
        return Ok(await _clientService.GetStatsAsync(id, ct));
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateClientRequest request, CancellationToken ct)
    {
        var id = await _clientService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClientRequest request, CancellationToken ct)
    {
        await _clientService.UpdateAsync(id, request, ct);
        return NoContent();
    }

    /// <summary>Archive un client (le conserve avec ses factures, mais le retire de la liste active).</summary>
    [HttpPatch("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        await _clientService.ArchiveAsync(id, ct);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var guid))
            throw new BadRequestException("Identifiant client invalide.");

        await _clientService.DeleteAsync(guid, ct);
        return NoContent();
    }

    [HttpPost("import")]
    public async Task<ActionResult<int>> Import([FromBody] IEnumerable<CreateClientRequest> clients, CancellationToken ct)
    {
        return Ok(new { imported = await _clientService.ImportAsync(clients, ct) });
    }
}
