using System.Text.Json;
using System.Text.Json.Serialization;
using Factur.Application.DTOs;
using Factur.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Factur.Infrastructure.Services;

/// <summary>Persistance des préférences générales dans <c>settings.json</c> (racine des données).</summary>
public class SettingsService : ISettingsService
{
    private static readonly string[] AllowedLanguages = { "fr", "en" };
    private static readonly string[] AllowedThemes = { "light", "dark", "system" };
    private static readonly int[] AllowedFrequencies = { 5, 15, 30, 60, 360, 1440 };
    private static readonly int[] AllowedRetention = { 0, 3, 5, 10 };

    private readonly string _settingsFile;
    private readonly string _root;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public SettingsService(IConfiguration configuration)
    {
        var root = AppPaths.ResolveRoot(configuration["App:DataRoot"], configuration.GetConnectionString("DefaultConnection") ?? string.Empty);
        _root = root;
        _settingsFile = AppPaths.SettingsFile(root);
    }

    public async Task<AppSettings> GetAsync(CancellationToken ct = default)
    {
        var persisted = await ReadAsync(ct);
        return ToSettings(persisted);
    }

    public async Task<AppSettings> SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        var current = await ReadAsync(ct);
        current.Language = Normalize(settings.Language, AllowedLanguages, "fr");
        current.Theme = Normalize(settings.Theme, AllowedThemes, "light");
        current.AutoBackupEnabled = settings.AutoBackupEnabled;
        current.BackupFrequencyMinutes = NormalizeInt(settings.BackupFrequencyMinutes, AllowedFrequencies, 30);
        current.BackupRetentionCount = NormalizeInt(settings.BackupRetentionCount, AllowedRetention, 5);
        current.BackupLocation = NormalizeLocation(settings.BackupLocation);
        current.SplashEnabled = settings.SplashEnabled;
        await WriteAsync(current, ct);
        return ToSettings(current);
    }

    public async Task<BackupState> GetBackupStateAsync(CancellationToken ct = default)
    {
        var persisted = await ReadAsync(ct);
        return new BackupState
        {
            LastBackupAt = persisted.LastBackupAt,
            LastBackupStatus = persisted.LastBackupStatus,
            LastBackupFileName = persisted.LastBackupFileName,
        };
    }

    public async Task SetBackupStateAsync(BackupState state, CancellationToken ct = default)
    {
        var persisted = await ReadAsync(ct);
        persisted.LastBackupAt = state.LastBackupAt;
        persisted.LastBackupStatus = state.LastBackupStatus;
        persisted.LastBackupFileName = state.LastBackupFileName;
        await WriteAsync(persisted, ct);
    }

    private async Task<PersistedSettings> ReadAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (!File.Exists(_settingsFile))
            {
                return CreateDefault();
            }

            try
            {
                var json = await File.ReadAllTextAsync(_settingsFile, ct);
                var parsed = JsonSerializer.Deserialize<PersistedSettings>(json, JsonOptions);
                if (parsed is null)
                {
                    return CreateDefault();
                }
                parsed.NormalizeDefaults(_root);
                return parsed;
            }
            catch (JsonException)
            {
                return CreateDefault();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task WriteAsync(PersistedSettings settings, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await WriteCoreAsync(settings, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task EnsurePersistedAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (!File.Exists(_settingsFile))
            {
                await WriteCoreAsync(CreateDefault(), ct);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task WriteCoreAsync(PersistedSettings settings, CancellationToken ct)
    {
        Directory.CreateDirectory(_root);
        settings.NormalizeDefaults(_root);
        var tmp = _settingsFile + ".tmp";
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await File.WriteAllTextAsync(tmp, json, ct);
        File.Move(tmp, _settingsFile, overwrite: true);
    }

    private PersistedSettings CreateDefault()
    {
        var settings = new PersistedSettings();
        settings.NormalizeDefaults(_root);
        return settings;
    }

    private static AppSettings ToSettings(PersistedSettings persisted) => new()
    {
        Language = persisted.Language,
        Theme = persisted.Theme,
        AutoBackupEnabled = persisted.AutoBackupEnabled,
        BackupFrequencyMinutes = persisted.BackupFrequencyMinutes,
        BackupRetentionCount = persisted.BackupRetentionCount,
        BackupLocation = persisted.BackupLocation,
        SplashEnabled = persisted.SplashEnabled,
    };

    private string NormalizeLocation(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return AppPaths.DefaultBackupDirectory(_root);
        }
        return Path.GetFullPath(location.Trim());
    }

    private static string Normalize(string? value, string[] allowed, string fallback)
        => allowed.Contains(value?.Trim(), StringComparer.OrdinalIgnoreCase) ? value!.Trim().ToLowerInvariant() : fallback;

    private static int NormalizeInt(int value, int[] allowed, int fallback)
        => allowed.Contains(value) ? value : fallback;
}

/// <summary>Fichier <c>settings.json</c> : préférences + état de sauvegarde (interne).</summary>
internal class PersistedSettings : AppSettings
{
    public DateTime? LastBackupAt { get; set; }
    public string? LastBackupStatus { get; set; }
    public string? LastBackupFileName { get; set; }

    public void NormalizeDefaults(string root)
    {
        if (!IsValidLanguage(Language)) Language = "fr";
        if (!IsValidTheme(Theme)) Theme = "light";
        if (BackupFrequencyMinutes is not (5 or 15 or 30 or 60 or 360 or 1440)) BackupFrequencyMinutes = 30;
        if (BackupRetentionCount is not (0 or 3 or 5 or 10)) BackupRetentionCount = 5;
        if (string.IsNullOrWhiteSpace(BackupLocation)) BackupLocation = AppPaths.DefaultBackupDirectory(root);
        if (LastBackupStatus is not (null or "ok" or "failed")) LastBackupStatus = null;
    }

    private static bool IsValidLanguage(string value)
        => value is "fr" or "en";

    private static bool IsValidTheme(string value)
        => value is "light" or "dark" or "system";
}
