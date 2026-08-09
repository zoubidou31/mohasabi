using Factur.Domain.Common;
using Factur.Domain.Enums;

namespace Factur.Domain.Entities;

/// <summary>Produit ou service vendu.</summary>
public class Product : BaseEntity
{
    public string Reference { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public Guid? CategoryId { get; set; }
    public decimal DefaultPrice { get; set; }
    public TVARate DefaultTVARate { get; set; } = TVARate.Normal;
    public bool IsService { get; set; }
    public bool IsActive { get; set; } = true;

    public Category? CategoryRef { get; set; }
}
