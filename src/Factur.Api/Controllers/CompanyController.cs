using Factur.Application.DTOs;
using Factur.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Factur.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CompanyController : ControllerBase
{
    private readonly ICompanyService _companyService;

    public CompanyController(ICompanyService companyService)
    {
        _companyService = companyService;
    }

    [HttpGet]
    public async Task<ActionResult<CompanyDto>> Get(CancellationToken ct)
    {
        return Ok(await _companyService.GetAsync(ct));
    }

    [HttpPut]
    public async Task<ActionResult<CompanyDto>> Save([FromBody] UpdateCompanyRequest request, CancellationToken ct)
    {
        return Ok(await _companyService.SaveAsync(request, ct));
    }
}
