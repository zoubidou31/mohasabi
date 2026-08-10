using Factur.Application.Common.Exceptions;
using Factur.Application.Common.Mapping;
using Factur.Application.DTOs;
using Factur.Application.Interfaces;
using Factur.Domain.Entities;
using Factur.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Factur.Infrastructure.Services;

public class ProductService : IProductService
{
    private readonly ApplicationDbContext _context;
    private readonly IValidator<CreateProductRequest> _validator;
    private readonly IAuditLogger _auditLogger;

    public ProductService(ApplicationDbContext context, IValidator<CreateProductRequest> validator, IAuditLogger auditLogger)
    {
        _context = context;
        _validator = validator;
        _auditLogger = auditLogger;
    }

    public async Task<Guid> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        if (await _context.Products.AnyAsync(p => p.Reference == request.Reference.Trim(), ct))
        {
            throw new BusinessRuleException("Cette référence existe déjà.");
        }

        var product = new Product();
        Apply(request, product);
        _context.Products.Add(product);
        await _context.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Product", product.Id.ToString(), "Création", new { product.Reference, product.Name }, ct);
        return product.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        var product = await _context.Products.FindAsync([id], ct)
            ?? throw new NotFoundException("Produit introuvable.");

        if (await _context.Products.AnyAsync(p => p.Reference == request.Reference.Trim() && p.Id != id, ct))
        {
            throw new BusinessRuleException("Cette référence existe déjà.");
        }

        Apply(request, product);
        product.IsActive = request.IsActive;
        product.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Product", id.ToString(), "Modification", new { product.Reference }, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var product = await _context.Products.FindAsync([id], ct)
            ?? throw new NotFoundException("Produit introuvable.");
        _context.Products.Remove(product);
        await _context.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Product", id.ToString(), "Suppression", new { product.Reference }, ct);
    }

    public async Task<ProductDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var product = await _context.Products.AsNoTracking().Include(p => p.CategoryRef).FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Produit introuvable.");
        return product.ToDto();
    }

    public async Task<PagedResult<ProductDto>> GetPagedAsync(string? search = null, bool includeInactive = false, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        IQueryable<Product> q = _context.Products.AsNoTracking().Include(p => p.CategoryRef);
        if (!includeInactive)
        {
            q = q.Where(p => p.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(p => p.Name.ToLower().Contains(s)
                             || p.Reference.ToLower().Contains(s)
                             || (p.Description != null && p.Description.ToLower().Contains(s)));
        }

        var totalCount = await q.CountAsync(ct);
        page = Math.Max(1, page);
        pageSize = Math.Max(1, pageSize);

        var products = await q
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<ProductDto>
        {
            Items = products.Select(p => p.ToDto()).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct = default)
    {
        return await _context.Categories.AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => c.Name)
            .OrderBy(c => c)
            .ToListAsync(ct);
    }

    public async Task<int> ImportAsync(IEnumerable<CreateProductRequest> products, CancellationToken ct = default)
    {
        var list = products.ToList();
        if (list.Count == 0)
        {
            return 0;
        }

        var imported = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var request in list)
        {
            var validation = await _validator.ValidateAsync(request, ct);
            if (!validation.IsValid || string.IsNullOrWhiteSpace(request.Reference))
            {
                continue;
            }

            var reference = request.Reference.Trim();
            if (!seen.Add(reference))
            {
                continue;
            }

            var exists = await _context.Products.AnyAsync(p => p.Reference == reference, ct);
            if (exists)
            {
                continue;
            }

            var product = new Product();
            Apply(request, product);
            _context.Products.Add(product);
            imported++;
        }

        await _context.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Product", "import", "Import Excel", new { imported }, ct);
        return imported;
    }

    private static void Apply(CreateProductRequest request, Product product)
    {
        product.Reference = request.Reference.Trim();
        product.Name = request.Name.Trim();
        product.Description = request.Description?.Trim();
        product.Category = request.Category?.Trim();
        product.CategoryId = request.CategoryId;
        product.DefaultPrice = request.DefaultPrice;
        product.DefaultTVARate = request.DefaultTVARate;
        product.IsService = request.IsService;
    }
}
