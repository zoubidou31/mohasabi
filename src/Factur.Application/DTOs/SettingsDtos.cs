namespace Factur.Application.DTOs;

/// <summary>Préférences générales de l'application (page Options).</summary>
public class AppSettings
{
    public string Language { get; set; } = "fr";
    public string Theme { get; set; } = "light";
    public bool AutoBackupEnabled { get; set; } = true;
    public int BackupFrequencyMinutes { get; set; } = 30;
    public int BackupRetentionCount { get; set; } = 5;
    public string BackupLocation { get; set; } = string.Empty;
    public bool SplashEnabled { get; set; } = true;

    // Typographie de l'interface (UI de l'application) — n'affecte pas les exports.
    public string AppFontFamily { get; set; } = "Inter";
    public string InterfaceFontSize { get; set; } = "medium";

    // Typographie des documents exportés (PDF / Word / Excel).
    public string DocFontFamily { get; set; } = "Inter";
    public double DocBaseFontSize { get; set; } = 11;
    public double DocTableFontSize { get; set; } = 9;
    public double DocHeaderFontSize { get; set; } = 13;
    public double DocFooterFontSize { get; set; } = 9;
}

/// <summary>État de la sauvegarde (écrit par le service de sauvegarde, lu par l'API).</summary>
public class BackupState
{
    public DateTime? LastBackupAt { get; set; }
    public string? LastBackupStatus { get; set; }
    public string? LastBackupFileName { get; set; }
}

/// <summary>Informations sur une sauvegarde existante.</summary>
public class BackupInfo
{
    public string FileName { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Résultat d'une sauvegarde déclenchée manuellement.</summary>
public class BackupRunResult
{
    public bool Success { get; set; }
    public string? FileName { get; set; }
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Error { get; set; }
}

/// <summary>État global du système de sauvegarde.</summary>
public class BackupStatusDto
{
    public bool AutoBackupEnabled { get; set; }
    public int BackupFrequencyMinutes { get; set; }
    public string BackupLocation { get; set; } = string.Empty;
    public DateTime? LastBackupAt { get; set; }
    public string? LastBackupStatus { get; set; }
    public string? LastBackupFileName { get; set; }
    public int BackupCount { get; set; }
    public long TotalSize { get; set; }
}

/// <summary>Requête de restauration.</summary>
public class RestoreRequest
{
    public string FileName { get; set; } = string.Empty;
}

/// <summary>Résultat d'une restauration.</summary>
public class RestoreResult
{
    public bool Success { get; set; }
    public bool RequiresRestart { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}
