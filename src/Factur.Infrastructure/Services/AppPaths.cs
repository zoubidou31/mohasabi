namespace Factur.Infrastructure.Services;

/// <summary>Configuration de l'emplacement des données de l'application.</summary>
public class AppOptions
{
    /// <summary>Racine des données (si vide : déduite de la chaîne de connexion).</summary>
    public string? DataRoot { get; set; }
}

/// <summary>Résolution des chemins données / sauvegardes / restauration.</summary>
public static class AppPaths
{
    /// <summary>Racine des données (contient <c>data</c>, <c>settings.json</c>, <c>Backups</c>, <c>restore</c>).</summary>
    public static string ResolveRoot(string? configuredRoot, string connectionString)
    {
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            return Path.GetFullPath(configuredRoot);
        }

        var dbPath = ExtractDatabasePath(connectionString);
        if (!string.IsNullOrWhiteSpace(dbPath))
        {
            var dataDir = Path.GetDirectoryName(dbPath);
            var root = dataDir is null ? null : Path.GetDirectoryName(dataDir);
            if (!string.IsNullOrWhiteSpace(root))
            {
                return Path.GetFullPath(root);
            }
            if (!string.IsNullOrWhiteSpace(dataDir))
            {
                return Path.GetFullPath(dataDir);
            }
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "data"));
    }

    /// <summary>Fichier de préférences (racine des données).</summary>
    public static string SettingsFile(string root) => Path.Combine(root, "settings.json");

    /// <summary>Dossier par défaut des sauvegardes.</summary>
    public static string DefaultBackupDirectory(string root) => Path.Combine(root, "Backups");

    /// <summary>Dossier de préparation de la restauration.</summary>
    public static string RestoreDirectory(string root) => Path.Combine(root, "restore");

    /// <summary>Manifeste de restauration en attente (lu au démarrage par l'API).</summary>
    public static string RestoreManifest(string root) => Path.Combine(RestoreDirectory(root), "restore.json");

    /// <summary>Marqueur : dernier arrêt propre (écrit par le launcher).</summary>
    public static string CleanExitMarker(string root) => Path.Combine(root, "clean-exit.marker");

    /// <summary>Marqueur : redémarrage attendu après restauration.</summary>
    public static string RestartPendingMarker(string root) => Path.Combine(root, "restart-pending");

    /// <summary>Marqueur : mise à jour en attente (existant, utilisé par le launcher).</summary>
    public static string UpdatePendingMarker(string root) => Path.Combine(root, "update-pending");

    /// <summary>Extrait le chemin du fichier SQLite d'une chaîne de connexion EF Core.</summary>
    private static string? ExtractDatabasePath(string connectionString)
    {
        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (kv.Length == 2 && kv[0].Equals("Data Source", StringComparison.OrdinalIgnoreCase) && kv[1].Length > 0)
            {
                return kv[1];
            }
        }
        return null;
    }
}
