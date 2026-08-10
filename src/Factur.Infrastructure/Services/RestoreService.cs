using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Factur.Application.DTOs;
using Factur.Application.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Factur.Infrastructure.Services;

/// <summary>Restauration sécurisée : validation, sauvegarde d'urgence, application au prochain démarrage.</summary>
public class RestoreService : IRestoreService
{
    private readonly ISettingsService _settingsService;
    private readonly string _root;
    private readonly string _databasePath;
    private readonly string _uploadsPath;
    private readonly ILogger<RestoreService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public RestoreService(IConfiguration configuration, ISettingsService settingsService, IOptions<StorageOptions> storage, ILogger<RestoreService> logger)
    {
        _settingsService = settingsService;
        _root = AppPaths.ResolveRoot(configuration["App:DataRoot"], configuration.GetConnectionString("DefaultConnection") ?? string.Empty);
        _databasePath = ResolveDatabasePath(configuration);
        _uploadsPath = StoragePaths.ResolveUploads(storage.Value);
        _logger = logger;
    }

    public async Task<RestoreResult> RestoreAsync(RestoreRequest request, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        var tempDir = Path.Combine(Path.GetTempPath(), "mohasabi-restore-" + Guid.NewGuid().ToString("N"));
        try
        {
            var settings = await _settingsService.GetAsync(ct);
            var backupDir = settings.BackupLocation;
            var payloadPath = ResolveSafePath(backupDir, request.FileName);
            if (!File.Exists(payloadPath))
            {
                return new RestoreResult { Success = false, Error = "Sauvegarde introuvable." };
            }

            // 1. Validation complète de la sauvegarde choisie.
            var sha256 = await ComputeSha256Async(payloadPath, ct);
            var index = await ReadIndexAsync(backupDir, ct);
            var entry = index.Backups.FirstOrDefault(b => b.FileName == Path.GetFileName(payloadPath));
            if (entry is not null && !string.Equals(entry.Sha256, sha256, StringComparison.OrdinalIgnoreCase))
            {
                return new RestoreResult { Success = false, Error = "L'empreinte de la sauvegarde ne correspond pas à son index : restauration refusée." };
            }

            Directory.CreateDirectory(tempDir);
            ZipFile.ExtractToDirectory(payloadPath, tempDir, overwriteFiles: true);
            var extractedDb = Path.Combine(tempDir, "mohasabi.db");
            if (!File.Exists(extractedDb) || !await QuickCheckAsync(extractedDb, ct))
            {
                return new RestoreResult { Success = false, Error = "La sauvegarde est corrompue (base invalide) : restauration refusée." };
            }

            // 2. Sauvegarde d'urgence des données actuelles (chemin de retour possible).
            var emergencyName = await CreateEmergencyBackupAsync(ct);

            // 3. Copie de la sauvegarde dans le dossier de restauration (autonome).
            var restoreDir = AppPaths.RestoreDirectory(_root);
            Directory.CreateDirectory(restoreDir);
            var staged = Path.Combine(restoreDir, "restore-payload.zip");
            File.Copy(payloadPath, staged, overwrite: true);

            // 4. Manifeste de restauration en attente (appliqué au prochain démarrage de l'API).
            var manifest = new RestoreManifest
            {
                State = "pending",
                CreatedAt = DateTime.UtcNow,
                Payload = "restore-payload.zip",
                PayloadSha256 = sha256,
                EmergencyFile = emergencyName,
                BackupFileName = Path.GetFileName(payloadPath),
            };
            await WriteManifestAsync(manifest, ct);

            _logger.LogInformation("Restauration préparée (sauvegarde d'urgence : {Emergency}).", emergencyName);
            return new RestoreResult
            {
                Success = true,
                RequiresRestart = true,
                Message = "Restauration prête. L'application va redémarrer.",
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec de la préparation de la restauration.");
            return new RestoreResult { Success = false, Error = ex.Message };
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch { }
            _gate.Release();
        }
    }

    /// <summary>Applique une restauration en attente (appelé au démarrage, avant l'ouverture de la base).</summary>
    public async Task ApplyPendingAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var manifestPath = AppPaths.RestoreManifest(_root);
            if (!File.Exists(manifestPath))
            {
                return;
            }

            RestoreManifest? manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<RestoreManifest>(await File.ReadAllTextAsync(manifestPath, ct), JsonOptions);
            }
            catch (JsonException)
            {
                _logger.LogWarning("Manifeste de restauration illisible : ignoré.");
                return;
            }

            if (manifest is null || manifest.State != "pending")
            {
                return;
            }

            var restoreDir = AppPaths.RestoreDirectory(_root);
            var staged = Path.Combine(restoreDir, manifest.Payload ?? "restore-payload.zip");
            var tempDir = Path.Combine(Path.GetTempPath(), "mohasabi-apply-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tempDir);

                if (!File.Exists(staged))
                {
                    throw new InvalidOperationException("Sauvegarde de restauration introuvable.");
                }

                var stagedSha = await ComputeSha256Async(staged, ct);
                if (!string.Equals(stagedSha, manifest.PayloadSha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("L'empreinte de la sauvegarde a changé : restauration annulée.");
                }

                ZipFile.ExtractToDirectory(staged, tempDir, overwriteFiles: true);
                var extractedDb = Path.Combine(tempDir, "mohasabi.db");
                if (!File.Exists(extractedDb) || !await QuickCheckAsync(extractedDb, ct))
                {
                    throw new InvalidOperationException("La base restaurée est invalide.");
                }

                // Sauvegarde des données actuelles avant remplacement (journal de retour).
                await MoveCurrentToPreviousAsync(ct);

                // Application.
                await ApplyExtractedAsync(tempDir, ct);
                manifest.State = "applied";
                manifest.AppliedAt = DateTime.UtcNow;
                await WriteManifestAsync(manifest, ct);
                _logger.LogInformation("Restauration appliquée avec succès ({Backup}).", manifest.BackupFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Échec de l'application de la restauration ; retour arrière vers la sauvegarde d'urgence.");
                await TryRollbackAsync(manifest, ct);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, recursive: true);
                    }
                }
                catch { }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<string> CreateEmergencyBackupAsync(CancellationToken ct)
    {
        var emergencyDir = Path.Combine(AppPaths.RestoreDirectory(_root), "emergency");
        Directory.CreateDirectory(emergencyDir);
        var tempDir = Path.Combine(Path.GetTempPath(), "mohasabi-emergency-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var dbCopy = Path.Combine(tempDir, "mohasabi.db");
            var sourceConn = new SqliteConnection($"Data Source={_databasePath};Mode=ReadOnly;Cache=Shared;Pooling=False");
            var destConn = new SqliteConnection($"Data Source={dbCopy};Pooling=False");
            try
            {
                await sourceConn.OpenAsync(ct);
                await destConn.OpenAsync(ct);
                sourceConn.BackupDatabase(destConn);
            }
            finally
            {
                await destConn.DisposeAsync();
                await sourceConn.DisposeAsync();
            }

            if (Directory.Exists(_uploadsPath))
            {
                var uploadsTarget = Path.Combine(tempDir, "uploads");
                Directory.CreateDirectory(uploadsTarget);
                foreach (var file in Directory.EnumerateFiles(_uploadsPath, "*", SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(_uploadsPath, file);
                    var target = Path.Combine(uploadsTarget, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(file, target, overwrite: true);
                }
            }

            var settingsFile = AppPaths.SettingsFile(_root);
            if (File.Exists(settingsFile))
            {
                File.Copy(settingsFile, Path.Combine(tempDir, "settings.json"), overwrite: true);
            }

            var fileName = $"emergency-{DateTime.Now:yyyyMMdd-HHmmss}.zip";
            var zipPath = Path.Combine(emergencyDir, fileName);
            ZipFile.CreateFromDirectory(tempDir, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);

            // Conserve les 3 dernières sauvegardes d'urgence.
            var existing = Directory.EnumerateFiles(emergencyDir, "emergency-*.zip")
                .OrderByDescending(p => p)
                .ToList();
            foreach (var old in existing.Skip(3))
            {
                try
                {
                    File.Delete(old);
                }
                catch { }
            }

            return fileName;
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch { }
        }
    }

    private async Task MoveCurrentToPreviousAsync(CancellationToken ct)
    {
        var previousDir = Path.Combine(AppPaths.RestoreDirectory(_root), "previous", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(previousDir);

        if (File.Exists(_databasePath))
        {
            var target = Path.Combine(previousDir, "mohasabi.db");
            File.Move(_databasePath, target, overwrite: true);
        }
        if (Directory.Exists(_uploadsPath))
        {
            var target = Path.Combine(previousDir, "uploads");
            CopyDirectory(_uploadsPath, target);
        }
        var settingsFile = AppPaths.SettingsFile(_root);
        if (File.Exists(settingsFile))
        {
            File.Copy(settingsFile, Path.Combine(previousDir, "settings.json"), overwrite: true);
        }
        await Task.CompletedTask;
    }

    private async Task ApplyExtractedAsync(string tempDir, CancellationToken ct)
    {
        var dbSource = Path.Combine(tempDir, "mohasabi.db");
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
        RemoveStaleWalFiles();
        File.Copy(dbSource, _databasePath, overwrite: true);

        var uploadsSource = Path.Combine(tempDir, "uploads");
        if (Directory.Exists(uploadsSource))
        {
            Directory.CreateDirectory(_uploadsPath);
            CopyDirectory(uploadsSource, _uploadsPath);
        }

        var settingsSource = Path.Combine(tempDir, "settings.json");
        if (File.Exists(settingsSource))
        {
            File.Copy(settingsSource, AppPaths.SettingsFile(_root), overwrite: true);
        }
        await Task.CompletedTask;
    }

    private async Task TryRollbackAsync(RestoreManifest manifest, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(manifest.EmergencyFile))
            {
                throw new InvalidOperationException("Aucune sauvegarde d'urgence disponible.");
            }

            var emergencyZip = Path.Combine(AppPaths.RestoreDirectory(_root), "emergency", manifest.EmergencyFile);
            if (!File.Exists(emergencyZip))
            {
                throw new InvalidOperationException("Sauvegarde d'urgence introuvable.");
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "mohasabi-rollback-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                ZipFile.ExtractToDirectory(emergencyZip, tempDir, overwriteFiles: true);
                var dbCopy = Path.Combine(tempDir, "mohasabi.db");
                if (!File.Exists(dbCopy) || !await QuickCheckAsync(dbCopy, ct))
                {
                    throw new InvalidOperationException("La sauvegarde d'urgence est invalide.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
                RemoveStaleWalFiles();
                File.Copy(dbCopy, _databasePath, overwrite: true);

                var uploadsSource = Path.Combine(tempDir, "uploads");
                if (Directory.Exists(uploadsSource))
                {
                    Directory.CreateDirectory(_uploadsPath);
                    CopyDirectory(uploadsSource, _uploadsPath);
                }

                var settingsSource = Path.Combine(tempDir, "settings.json");
                if (File.Exists(settingsSource))
                {
                    File.Copy(settingsSource, AppPaths.SettingsFile(_root), overwrite: true);
                }
            }
            finally
            {
                try
                {
                    Directory.Delete(tempDir, recursive: true);
                }
                catch { }
            }

            manifest.State = "rolled-back";
            manifest.RolledBackAt = DateTime.UtcNow;
            await WriteManifestAsync(manifest, ct);
            _logger.LogWarning("Retour arrière effectué vers la sauvegarde d'urgence {Emergency}.", manifest.EmergencyFile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Le retour arrière a lui-même échoué. Les données précédentes restent dans restore/previous.");
            manifest.State = "failed";
            try
            {
                await WriteManifestAsync(manifest, ct);
            }
            catch { }
        }
    }

    private async Task WriteManifestAsync(RestoreManifest manifest, CancellationToken ct)
    {
        var restoreDir = AppPaths.RestoreDirectory(_root);
        Directory.CreateDirectory(restoreDir);
        var path = AppPaths.RestoreManifest(_root);
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        await File.WriteAllTextAsync(path + ".tmp", json, ct);
        File.Move(path + ".tmp", path, overwrite: true);
    }

    private static async Task<BackupIndex> ReadIndexAsync(string backupDir, CancellationToken ct)
    {
        var indexFile = Path.Combine(backupDir, "index.json");
        if (!File.Exists(indexFile))
        {
            return new BackupIndex();
        }
        try
        {
            return JsonSerializer.Deserialize<BackupIndex>(await File.ReadAllTextAsync(indexFile, ct), JsonOptions) ?? new BackupIndex();
        }
        catch (JsonException)
        {
            return new BackupIndex();
        }
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
    {
        var hash = SHA256.HashData(await File.ReadAllBytesAsync(path, ct));
        return Convert.ToHexString(hash);
    }

    private static async Task<bool> QuickCheckAsync(string dbPath, CancellationToken ct)
    {
        try
        {
            await using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly;Pooling=False");
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA quick_check;";
            var result = await cmd.ExecuteScalarAsync(ct);
            return string.Equals(result as string, "ok", StringComparison.OrdinalIgnoreCase);
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    private static string ResolveSafePath(string backupDir, string fileName)
    {
        var root = Path.GetFullPath(backupDir);
        var fullPath = Path.GetFullPath(Path.Combine(root, Path.GetFileName(fileName)));
        if (!string.Equals(Path.GetDirectoryName(fullPath), root, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Nom de fichier invalide.");
        }
        return fullPath;
    }

    private static string ResolveDatabasePath(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (kv.Length == 2 && kv[0].Equals("Data Source", StringComparison.OrdinalIgnoreCase) && kv[1].Length > 0)
            {
                return kv[1];
            }
        }
        return Path.Combine(AppContext.BaseDirectory, "data", "mohasabi.db");
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var target = Path.Combine(targetDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    /// <summary>Supprime les fichiers WAL/SHM résiduels d'une session précédente pour ne
    /// pas les appliquer à tort à la base restaurée.</summary>
    private void RemoveStaleWalFiles()
    {
        try
        {
            foreach (var suffix in new[] { "-wal", "-shm" })
            {
                var path = _databasePath + suffix;
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
        catch
        {
            // Non bloquant : l'ouverture de la base vérifiera son intégrité.
        }
    }
}

/// <summary>Manifeste de restauration (dossier de restauration).</summary>
internal class RestoreManifest
{
    public string State { get; set; } = "pending"; // pending | applied | rolled-back | failed
    public DateTime CreatedAt { get; set; }
    public DateTime? AppliedAt { get; set; }
    public DateTime? RolledBackAt { get; set; }
    public string? Payload { get; set; }
    public string? PayloadSha256 { get; set; }
    public string? EmergencyFile { get; set; }
    public string? BackupFileName { get; set; }
}
