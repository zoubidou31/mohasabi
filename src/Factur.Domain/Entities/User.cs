using Factur.Domain.Common;
using Factur.Domain.Enums;

namespace Factur.Domain.Entities;

/// <summary>Utilisateur de l'application.</summary>
public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Utilisateur;
    public bool IsActive { get; set; } = true;
}
