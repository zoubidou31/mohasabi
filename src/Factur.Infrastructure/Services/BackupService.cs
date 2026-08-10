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

/// <summary>Sauvegardes de la base, des fichiers téléversés et des préférences dans un ZIP vérifié.</summary>
public class BackupService : IBackupService
{
    private readonly ISettingsService _settingsService;
    private readonly string _databasePath;
    private readonly string _uploadsPath;
    private readonly string _root;
    private readonly ILogger<BackupService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public BackupService(IConfiguration configuration, ISettingsService settingsService, IOptions<StorageOptions> storage, ILogger<BackupService> logger)
    {
        _settingsService = settingsService;
        _databasePath = ResolveDatabasePath(configuration);
        _root = AppPaths.ResolveRoot(configuration["App:DataRoot"], configuration.GetConnectionString("DefaultConnection") ?? string.Empty);
        _uploadsPath = StoragePaths.ResolveUploads(storage.Value);
        _logger = logger;
    }

    public async Task<BackupRunResult> CreateAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        var tempDir = Path.Combine(Path.GetTempPath(), "mohasabi-backup-" + Guid.NewGuid().ToString("N"));
        try
        {
            var settings = await _settingsService.GetAsync(ct);
            var backupDir = settings.BackupLocation;
            Directory.CreateDirectory(backupDir);

            var now = DateTime.Now;
            var fileName = $"mohasabi-backup-{now:yyyyMMdd-HHmmss}.zip";
            var zipPath = Path.Combine(backupDir, fileName);

            Directory.CreateDirectory(tempDir);

            // 1. Copie SQLite en ligne (état cohérent même si l'API est ouverte).
            var backupDbPath = Path.Combine(tempDir, "mohasabi.db");
            await BackupDatabaseAsync(backupDbPath, ct);

            // 2. Fichiers téléversés (logo, tampon, signature...).
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

            // 3. Préférences (settings.json).
            var settingsFile = AppPaths.SettingsFile(_root);
            if (File.Exists(settingsFile))
            {
                File.Copy(settingsFile, Path.Combine(tempDir, "settings.json"), overwrite: true);
            }

            // 4. Vérification d'intégrité de la base avant archivage.
            var integrity = await QuickCheckAsync(backupDbPath, ct);
            if (!integrity)
            {
                throw new InvalidOperationException("La base sauvegardée n'a pas passé le contrôle d'intégrité.");
            }

            // 5. Archivage ZIP.
            ZipFile.CreateFromDirectory(tempDir, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);

            // 6. Vérification de l'archive (liste des fichiers + empreinte SHA-256).
            var sha256 = await VerifyZipAsync(zipPath, ct);
            var size = new FileInfo(zipPath).Length;
            var createdAt = now.ToUniversalTime();

            var index = await ReadIndexAsync(backupDir, ct);
            index.Backups.Add(new BackupEntry { FileName = fileName, CreatedAt = createdAt, Size = size, Sha256 = sha256 });
            await WriteIndexAsync(backupDir, index, ct);

            // 7. Rétention.
            await ApplyRetentionAsync(backupDir, settings.BackupRetentionCount, ct);

            await _settingsService.SetBackupStateAsync(new BackupState
            {
                LastBackupAt = DateTime.UtcNow,
                LastBackupStatus = "ok",
                LastBackupFileName = fileName,
            }, ct);

            _logger.LogInformation("Sauvegarde créée : {FileName} ({Size} octets, sha256 {Sha256})", fileName, size, sha256);
            return new BackupRunResult { Success = true, FileName = fileName, Size = size, CreatedAt = createdAt };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec de la sauvegarde.");
            await TryRecordFailureAsync(ct);
            return new BackupRunResult { Success = false, Error = ex.Message };
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
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<BackupInfo>> ListAsync(CancellationToken ct = default)
    {
        var settings = await _settingsService.GetAsync(ct);
        var index = await ReadIndexAsync(settings.BackupLocation, ct);
        return index.Backups
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BackupInfo { FileName = b.FileName, Size = b.Size, CreatedAt = b.CreatedAt })
            .ToList();
    }

    public async Task<BackupStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var settings = await _settingsService.GetAsync(ct);
        var state = await _settingsService.GetBackupStateAsync(ct);
        var backups = await ListAsync(ct);
        return new BackupStatusDto
        {
            AutoBackupEnabled = settings.AutoBackupEnabled,
            BackupFrequencyMinutes = settings.BackupFrequencyMinutes,
            BackupLocation = settings.BackupLocation,
            LastBackupAt = state.LastBackupAt,
            LastBackupStatus = state.LastBackupStatus,
            LastBackupFileName = state.LastBackupFileName,
            BackupCount = backups.Count,
            TotalSize = backups.Sum(b => b.Size),
        };
    }

    public async Task DeleteAsync(string fileName, CancellationToken ct = default)
    {
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(name) || !string.Equals(name, fileName, StringComparison.Ordinal))
        {
            throw new ArgumentException("Nom de fichier invalide.");
        }

        var settings = await _settingsService.GetAsync(ct);
        var backupDir = Path.GetFullPath(settings.BackupLocation);
        var index = await ReadIndexAsync(backupDir, ct);
        var entry = index.Backups.FirstOrDefault(b => string.Equals(b.FileName, name, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            throw new FileNotFoundException("Sauvegarde introuvable.", fileName);
        }

        var fullPath = Path.Combine(backupDir, entry.FileName);
        if (!string.Equals(Path.GetDirectoryName(fullPath), backupDir, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Nom de fichier invalide.");
        }

        File.Delete(fullPath);
        index.Backups.Remove(entry);
        await WriteIndexAsync(backupDir, index, ct);
    }

    private async Task BackupDatabaseAsync(string destinationPath, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var sourceConn = new SqliteConnection($"Data Source={_databasePath};Mode=ReadOnly;Cache=Shared;Pooling=False");
        var destConn = new SqliteConnection($"Data Source={destinationPath};Pooling=False");
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

    private async Task<string> VerifyZipAsync(string zipPath, CancellationToken ct)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "mohasabi.db", "settings.json" };
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName.Replace('\\', '/').TrimStart('/');
            if (expected.Contains(name))
            {
                found.Add(name);
            }
        }

        foreach (var required in expected)
        {
            if (!found.Contains(required))
            {
                throw new InvalidOperationException($"Archive incomplète : « {required} » manquant.");
            }
        }

        var sha = SHA256.HashData(await File.ReadAllBytesAsync(zipPath, ct));
        return Convert.ToHexString(sha);
    }

    private async Task ApplyRetentionAsync(string backupDir, int retentionCount, CancellationToken ct)
    {
        if (retentionCount <= 0)
        {
            return;
        }

        var index = await ReadIndexAsync(backupDir, ct);
        var ordered = index.Backups.OrderByDescending(b => b.CreatedAt).ToList();
        var toDelete = ordered.Skip(retentionCount).ToList();
        foreach (var entry in toDelete)
        {
            var path = Path.Combine(backupDir, entry.FileName);
            try
            {
                File.Delete(path);
                index.Backups.Remove(entry);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Impossible de supprimer la sauvegarde {FileName}", entry.FileName);
            }
        }
        await WriteIndexAsync(backupDir, index, ct);
    }

    private async Task TryRecordFailureAsync(CancellationToken ct)
    {
        try
        {
            await _settingsService.SetBackupStateAsync(new BackupState
            {
                LastBackupAt = DateTime.UtcNow,
                LastBackupStatus = "failed",
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible d'enregistrer l'échec de sauvegarde.");
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

    private static async Task<BackupIndex> ReadIndexAsync(string backupDir, CancellationToken ct)
    {
        var indexFile = Path.Combine(backupDir, "index.json");
        if (!File.Exists(indexFile))
        {
            return new BackupIndex();
        }

        try
        {
            var json = await File.ReadAllTextAsync(indexFile, ct);
            return JsonSerializer.Deserialize<BackupIndex>(json, JsonOptions) ?? new BackupIndex();
        }
        catch (JsonException)
        {
            return new BackupIndex();
        }
    }

    private static async Task WriteIndexAsync(string backupDir, BackupIndex index, CancellationToken ct)
    {
        Directory.CreateDirectory(backupDir);
        var indexFile = Path.Combine(backupDir, "index.json");
        var json = JsonSerializer.Serialize(index, JsonOptions);
        await File.WriteAllTextAsync(indexFile + ".tmp", json, ct);
        File.Move(indexFile + ".tmp", indexFile, overwrite: true);
    }
}

/// <summary>Index des sauvegardes (répertoire des sauvegardes).</summary>
internal class BackupIndex
{
    public List<BackupEntry> Backups { get; set; } = new();
}

/// <summary>Entrée de l'index des sauvegardes.</summary>
internal class BackupEntry
{
    public string FileName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long Size { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}
