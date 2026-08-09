using System.Text.Json;
using Factur.Application.Common.Interfaces;
using Factur.Application.Interfaces;
using Factur.Domain.Entities;
using Factur.Infrastructure.Persistence;

namespace Factur.Infrastructure.Services;

/// <summary>Enregistre les modifications dans la table AuditLog.</summary>
public class AuditLogger : IAuditLogger
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AuditLogger(ApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task LogAsync(string entityType, string entityId, string action, object? changedData = null, CancellationToken ct = default)
    {
        var log = new AuditLog
        {
            UserId = _currentUser.UserId,
            UserName = _currentUser.Username,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            ChangedData = changedData is null
                ? null
                : JsonSerializer.Serialize(changedData, new JsonSerializerOptions { WriteIndented = false }),
            Timestamp = DateTime.UtcNow,
        };

        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync(ct);
    }
}
