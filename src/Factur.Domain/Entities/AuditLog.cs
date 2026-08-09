using Factur.Domain.Common;

namespace Factur.Domain.Entities;

/// <summary>Traçabilité de toute modification (audit trail).</summary>
public class AuditLog : BaseEntity
{
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? ChangedData { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
