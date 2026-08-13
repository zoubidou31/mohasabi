using System.Net.Http.Json;
using System.Reflection;
using Factur.Application.DTOs;
using Factur.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace Factur.Infrastructure.Services;

/// <summary>Options de stockage des fichiers téléversés.</summary>
public class StorageOptions
{
    public string? UploadsPath { get; set; }
}

/// <summary>Options de mise à jour.</summary>
public class UpdateOptions
{
    public string? ManifestUrl { get; set; }
}

/// <summary>Résout les chemins de stockage (par défaut : dossier de l'application).</summary>
public static class StoragePaths
{
    public static string ResolveUploads(StorageOptions options)
        => string.IsNullOrWhiteSpace(options.UploadsPath)
            ? Path.Combine(AppContext.BaseDirectory, "uploads")
            : Path.GetFullPath(options.UploadsPath);
}

/// <summary>Manifest de mise à jour publié (version.json).</summary>
public class UpdateManifest
{
    public string Version { get; init; } = "";
    public string? DownloadUrl { get; init; }
    public string? Sha256 { get; init; }
    public string? ReleaseNotes { get; init; }
    public long? SizeBytes { get; init; }
}

/// <summary>Phases d'une installation de mise à jour.</summary>
public enum UpdateInstallPhase
{
    Idle = 0,
    Downloading = 1,
    Verifying = 2,
    Launching = 3,
    Failed = 4,
}

/// <summary>Traceur en mémoire de l'installation en cours, interrogé par le front.</summary>
public static class UpdateInstallTracker
{
    private static readonly object Gate = new();

    public static UpdateInstallPhase Phase { get; private set; } = UpdateInstallPhase.Idle;
    public static long DownloadedBytes { get; private set; }
    public static long? TotalBytes { get; private set; }
    public static string? Message { get; private set; }
    public static string? Error { get; private set; }

    public static void Reset()
    {
        lock (Gate)
        {
            Phase = UpdateInstallPhase.Idle;
            DownloadedBytes = 0;
            TotalBytes = null;
            Message = null;
            Error = null;
        }
    }

    public static void Set(UpdateInstallPhase phase, string? message = null)
    {
        lock (Gate)
        {
            Phase = phase;
            if (!string.IsNullOrWhiteSpace(message)) Message = message;
        }
    }

    public static void SetProgress(long downloaded, long? total, string? message)
    {
        lock (Gate)
        {
            DownloadedBytes = downloaded;
            TotalBytes = total;
            if (!string.IsNullOrWhiteSpace(message)) Message = message;
        }
    }

    public static void Fail(string error)
    {
        lock (Gate)
        {
            Phase = UpdateInstallPhase.Failed;
            Error = error;
        }
    }

    public static UpdateInstallStatusDto Snapshot()
    {
        lock (Gate)
        {
            var percent = TotalBytes is > 0
                ? (int)Math.Clamp(DownloadedBytes * 100 / TotalBytes.Value, 0, 100)
                : (int?)null;

            return new UpdateInstallStatusDto
            {
                Phase = Phase.ToString().ToLowerInvariant(),
                DownloadedBytes = DownloadedBytes,
                TotalBytes = TotalBytes,
                Percent = percent,
                Message = Message,
                Error = Error,
            };
        }
    }
}

/// <summary>Vérifie et prépare les mises à jour de l'application.</summary>
public class UpdateService : IUpdateService
{
    private const int CopyBufferSize = 128 * 1024;

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(60),
    };

    private readonly IOptions<UpdateOptions> _options;

    public UpdateService(IOptions<UpdateOptions> options)
    {
        _options = options;
    }

    public string CurrentVersion => GetCurrentVersion();

    public UpdateInstallStatusDto GetInstallStatus() => UpdateInstallTracker.Snapshot();

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        var manifestUrl = _options.Value.ManifestUrl;
        if (string.IsNullOrWhiteSpace(manifestUrl))
        {
            return new UpdateCheckResult
            {
                CurrentVersion = CurrentVersion,
                Success = false,
                Message = "La vérification des mises à jour n'est pas configurée.",
            };
        }

        if (!IsAllowedUpdateUrl(manifestUrl))
        {
            return new UpdateCheckResult
            {
                CurrentVersion = CurrentVersion,
                Success = false,
                Message = "L'adresse de mise à jour n'est pas sécurisée : HTTPS requis (hôte local autorisé).",
            };
        }

        try
        {
            using var response = await Http.GetAsync(manifestUrl, ct);
            response.EnsureSuccessStatusCode();

            var manifest = await response.Content.ReadFromJsonAsync<UpdateManifest>(ct)
                           ?? throw new InvalidOperationException("Le manifest est vide.");

            Version.TryParse(manifest.Version, out var latest);
            Version.TryParse(CurrentVersion, out var current);

            var updateAvailable = latest is not null && (current is null || latest > current);

            return new UpdateCheckResult
            {
                CurrentVersion = CurrentVersion,
                LatestVersion = manifest.Version,
                UpdateAvailable = updateAvailable,
                DownloadUrl = manifest.DownloadUrl,
                Sha256 = manifest.Sha256,
                ReleaseNotes = manifest.ReleaseNotes,
                SizeBytes = manifest.SizeBytes,
                Success = true,
            };
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult
            {
                CurrentVersion = CurrentVersion,
                Success = false,
                Message = $"Impossible de vérifier les mises à jour : {ex.Message}",
            };
        }
    }

    /// <summary>Autorise les URLs de mise à jour :
    /// - loopback (tests/développement local) ;
    /// - HTTPS vers github.com, *.github.com, *.githubusercontent.com (production).
    /// </summary>
    public static bool IsAllowedUpdateUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.IsLoopback)
        {
            return true;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var host = uri.Host.ToLowerInvariant();
        return host == "github.com"
            || host == "www.github.com"
            || host.EndsWith(".github.com", StringComparison.Ordinal)
            || host.EndsWith(".githubusercontent.com", StringComparison.Ordinal);
    }

    public async Task<string> DownloadInstallerAsync(string downloadUrl, string? expectedSha256, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            throw new InvalidOperationException("URL de téléchargement manquante.");
        }

        if (!IsAllowedUpdateUrl(downloadUrl))
        {
            throw new InvalidOperationException("URL de téléchargement refusée : HTTPS GitHub requis.");
        }

        if (string.IsNullOrWhiteSpace(expectedSha256))
        {
            throw new InvalidOperationException("Empreinte SHA-256 attendue manquante : intégrité non vérifiable.");
        }

        var dir = Path.Combine(Path.GetTempPath(), "MohasabiUpdate");
        Directory.CreateDirectory(dir);

        var fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
        if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            fileName = $"Mohasabi Setup {CurrentVersion}.exe";
        }

        var target = Path.Combine(dir, fileName);

        UpdateInstallTracker.Reset();
        UpdateInstallTracker.Set(UpdateInstallPhase.Downloading, "Téléchargement de la mise à jour…");

        using var response = await Http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        UpdateInstallTracker.SetProgress(0, totalBytes, "Téléchargement de la mise à jour…");

        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using (var output = File.Create(target))
        {
            var buffer = new byte[CopyBufferSize];
            long downloaded = 0;
            while (true)
            {
                var read = await source.ReadAsync(buffer, ct);
                if (read <= 0) break;
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
                downloaded += read;
                UpdateInstallTracker.SetProgress(downloaded, totalBytes, "Téléchargement de la mise à jour…");
            }
        }

        UpdateInstallTracker.Set(UpdateInstallPhase.Verifying, "Vérification de l'intégrité du fichier…");

        var actualSha256 = await ComputeSha256Async(target, ct);
        if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            try { File.Delete(target); } catch { /* Ignoré. */ }
            UpdateInstallTracker.Fail("Intégrité du fichier de mise à jour invalide : l'empreinte SHA-256 ne correspond pas au manifest.");
            throw new InvalidOperationException(
                "Intégrité du fichier de mise à jour invalide : l'empreinte SHA-256 ne correspond pas au manifest.");
        }

        UpdateInstallTracker.Set(UpdateInstallPhase.Launching, "Démarrage de l'installation…");
        return target;
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await System.Security.Cryptography.SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash);
    }

    private static string GetCurrentVersion()
    {
        var assembly = Assembly.GetEntryAssembly();
        var info = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(info))
        {
            var plus = info.IndexOf('+');
            return plus > 0 ? info[..plus] : info;
        }

        return assembly?.GetName().Version?.ToString(3) ?? "1.0.4";
    }
}
