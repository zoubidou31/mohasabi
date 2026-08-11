namespace Factur.Application.DTOs;

public class UpdateCheckResult
{
    public string CurrentVersion { get; init; } = "";
    public string? LatestVersion { get; init; }
    public bool UpdateAvailable { get; init; }
    public string? DownloadUrl { get; init; }
    public string? Sha256 { get; init; }
    public string? ReleaseNotes { get; init; }
    public long? SizeBytes { get; init; }
    public bool Success { get; init; }
    public string? Message { get; init; }
}

/// <summary>État de téléchargement / installation d'une mise à jour (interrogé par le front).</summary>
public class UpdateInstallStatusDto
{
    public string Phase { get; init; } = "idle";
    public long DownloadedBytes { get; init; }
    public long? TotalBytes { get; init; }
    public int? Percent { get; init; }
    public string? Message { get; init; }
    public string? Error { get; init; }
}
