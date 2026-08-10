using Factur.Application.DTOs;
using Factur.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Factur.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetAll([FromQuery] string? search, [FromQuery] bool includeInactive, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Ok(await _productService.GetPagedAsync(search, includeInactive, page, pageSize, ct));
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetCategories(CancellationToken ct)
    {
        return Ok(await _productService.GetCategoriesAsync(ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> GetById(Guid id, CancellationToken ct)
    {
        return Ok(await _productService.GetByIdAsync(id, ct));
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateProductRequest request, CancellationToken ct)
    {
        var id = await _productService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductRequest request, CancellationToken ct)
    {
        await _productService.UpdateAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _productService.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("import")]
    public async Task<ActionResult<int>> Import([FromBody] IEnumerable<CreateProductRequest> products, CancellationToken ct)
    {
        return Ok(new { imported = await _productService.ImportAsync(products, ct) });
    }
}
