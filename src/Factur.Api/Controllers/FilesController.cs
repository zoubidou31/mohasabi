using Factur.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Factur.Api.Controllers;

[ApiController]
[Route("api/files")]
public class FilesController : ControllerBase
{
    private readonly IOptions<StorageOptions> _storage;

    public FilesController(IOptions<StorageOptions> storage)
    {
        _storage = storage;
    }

    /// <summary>Sert les fichiers téléversés (logo, tampon).</summary>
    [HttpGet("{fileName}")]
    [AllowAnonymous]
    public IActionResult Get(string fileName)
    {
        var safeName = Path.GetFileName(fileName);
        var fullPath = Path.Combine(StoragePaths.ResolveUploads(_storage.Value), safeName);

        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound(new { message = "Fichier introuvable." });
        }

        var extension = Path.GetExtension(safeName).ToLowerInvariant();
        var contentType = extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream",
        };

        return File(System.IO.File.ReadAllBytes(fullPath), contentType);
    }
}
