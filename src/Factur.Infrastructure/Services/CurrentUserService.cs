using System.Security.Claims;
using Factur.Application.Common.Interfaces;
using Factur.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace Factur.Infrastructure.Services;

/// <summary>Résout l'utilisateur courant de la requête HTTP (sans authentification, toujours vide).</summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public Guid? UserId =>
        Principal?.FindFirstValue(ClaimTypes.NameIdentifier) is { } id && Guid.TryParse(id, out var guid)
            ? guid
            : null;

    public string? Username => Principal?.FindFirstValue(ClaimTypes.Name);

    public UserRole? Role =>
        Principal?.FindFirstValue(ClaimTypes.Role) is { } role && Enum.TryParse<UserRole>(role, out var parsed)
            ? parsed
            : null;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
}
