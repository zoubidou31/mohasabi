using Factur.Domain.Enums;

namespace Factur.Application.Common.Interfaces;

/// <summary>Utilisateur courant de la requête HTTP.</summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Username { get; }
    UserRole? Role { get; }
    bool IsAuthenticated { get; }
}
