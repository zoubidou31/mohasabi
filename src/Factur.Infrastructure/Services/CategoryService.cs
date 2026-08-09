using Factur.Application.Common.Exceptions;
using Factur.Application.Common.Mapping;
using Factur.Application.DTOs;
using Factur.Application.Interfaces;
using Factur.Domain.Entities;
using Factur.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Factur.Infrastructure.Services;

public class CategoryService : ICategoryService
{
    private readonly ApplicationDbContext _context;

    public CategoryService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length < 2)
            throw new BusinessRuleException("Le nom de la catégorie est obligatoire (min. 2 caractères).");

        if (name.Length > 100)
            throw new BusinessRuleException("Le nom de la catégorie ne peut pas dépasser 100 caractères.");

        if (await _context.Categories.AnyAsync(c => c.Name == name, ct))
            throw new BusinessRuleException("Cette catégorie existe déjà.");

        var category = new Category
        {
            Name = name,
            Description = request.Description?.Trim(),
        };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync(ct);
        return category.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default)
    {
        var category = await _context.Categories.FindAsync([id], ct)
            ?? throw new NotFoundException("Catégorie introuvable.");

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length < 2)
            throw new BusinessRuleException("Le nom de la catégorie est obligatoire (min. 2 caractères).");

        if (name.Length > 100)
            throw new BusinessRuleException("Le nom de la catégorie ne peut pas dépasser 100 caractères.");

        if (await _context.Categories.AnyAsync(c => c.Name == name && c.Id != id, ct))
            throw new BusinessRuleException("Cette catégorie existe déjà.");

        category.Name = name;
        category.Description = request.Description?.Trim();
        category.IsActive = request.IsActive;
        category.UpdatedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var category = await _context.Categories.FindAsync([id], ct)
            ?? throw new NotFoundException("Catégorie introuvable.");

        var hasProducts = await _context.Products.AnyAsync(p => p.CategoryId == id, ct);
        if (hasProducts)
            throw new BusinessRuleException("Impossible de supprimer cette catégorie : elle est utilisée par des produits.");

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<CategoryDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var category = await _context.Categories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Catégorie introuvable.");

        return category.ToDto();
    }

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(bool? active = null, CancellationToken ct = default)
    {
        IQueryable<Category> q = _context.Categories.AsNoTracking();
        if (active.HasValue)
            q = q.Where(c => c.IsActive == active.Value);

        var categories = await q.OrderBy(c => c.Name).ToListAsync(ct);
        return categories.Select(c => c.ToDto()).ToList();
    }

    public async Task<int> GetProductCountAsync(Guid categoryId, CancellationToken ct = default)
    {
        return await _context.Products.CountAsync(p => p.CategoryId == categoryId, ct);
    }
}
