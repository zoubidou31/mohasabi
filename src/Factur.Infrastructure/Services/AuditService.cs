using Factur.Application.DTOs;
using Factur.Application.Interfaces;
using Factur.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Factur.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;

    public AuditService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AuditLogDto>> GetAsync(string? entityType = null, DateTime? from = null, DateTime? to = null, int limit = 200, CancellationToken ct = default)
    {
        var q = _context.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(entityType)) q = q.Where(l => l.EntityType == entityType);
        if (from.HasValue) q = q.Where(l => l.Timestamp >= from.Value);
        if (to.HasValue) q = q.Where(l => l.Timestamp <= to.Value);

        var logs = await q
            .OrderByDescending(l => l.Timestamp)
            .Take(Math.Clamp(limit, 1, 1000))
            .ToListAsync(ct);

        return logs.Select(l => new AuditLogDto
        {
            Id = l.Id,
            UserId = l.UserId,
            UserName = l.UserName,
            EntityType = l.EntityType,
            EntityId = l.EntityId,
            Action = l.Action,
            ChangedData = l.ChangedData,
            Timestamp = l.Timestamp,
        }).ToList();
    }
}
