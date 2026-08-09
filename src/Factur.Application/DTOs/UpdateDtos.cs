namespace Factur.Application.DTOs;

public class UpdateCheckResult
{
    public string CurrentVersion { get; init; } = "";
    public string? LatestVersion { get; init; }
    public bool UpdateAvailable { get; init; }
    public string? DownloadUrl { get; init; }
    public string? Sha256 { get; init; }
    public string? ReleaseNotes { get; init; }
    public bool Success { get; init; }
    public string? Message { get; init; }
}
