namespace Factur.Application.Interfaces;

/// <summary>Enregistre une entrée d'audit (traçabilité).</summary>
public interface IAuditLogger
{
    Task LogAsync(string entityType, string entityId, string action, object? changedData = null, CancellationToken ct = default);
}
