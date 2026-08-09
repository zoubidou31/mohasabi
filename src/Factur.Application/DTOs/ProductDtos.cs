using Factur.Domain.Enums;

namespace Factur.Application.DTOs;

public class ProductDto
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public decimal DefaultPrice { get; set; }
    public TVARate DefaultTVARate { get; set; } = TVARate.Normal;
    public bool IsService { get; set; }
    public bool IsActive { get; set; }
}

public class CreateProductRequest
{
    public string Reference { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public Guid? CategoryId { get; set; }
    public decimal DefaultPrice { get; set; }
    public TVARate DefaultTVARate { get; set; } = TVARate.Normal;
    public bool IsService { get; set; }
}

public class UpdateProductRequest : CreateProductRequest
{
    public bool IsActive { get; set; } = true;
}
