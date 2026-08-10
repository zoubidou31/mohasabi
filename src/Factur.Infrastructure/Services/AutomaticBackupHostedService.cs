using Factur.Application.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Factur.Infrastructure.Services;

/// <summary>Déclenche les sauvegardes automatiques selon la fréquence configurée.</summary>
public class AutomaticBackupHostedService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Après un échec de sauvegarde, on réessaie après ce court délai au lieu
    /// d'attendre la fréquence complète : une panne ponctuelle et récupérable
    /// ne doit pas laisser les données sans protection pendant toute la fréquence.
    /// </summary>
    private static readonly TimeSpan FailureRetryInterval = TimeSpan.FromMinutes(5);

    private readonly ISettingsService _settingsService;
    private readonly IBackupService _backupService;
    private readonly ILogger<AutomaticBackupHostedService> _logger;

    public AutomaticBackupHostedService(ISettingsService settingsService, IBackupService backupService, ILogger<AutomaticBackupHostedService> logger)
    {
        _settingsService = settingsService;
        _backupService = backupService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Un premier contrôle rapide peu après le démarrage, puis toutes les minutes.
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TryRunIfDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du contrôle de sauvegarde automatique.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task TryRunIfDueAsync(CancellationToken ct)
    {
        var settings = await _settingsService.GetAsync(ct);
        if (!settings.AutoBackupEnabled)
        {
            return;
        }

        var state = await _settingsService.GetBackupStateAsync(ct);
        if (!IsDue(DateTime.UtcNow, state.LastBackupAt, state.LastBackupStatus, settings.BackupFrequencyMinutes))
        {
            return;
        }

        var result = await _backupService.CreateAsync(ct);
        if (result.Success)
        {
            _logger.LogInformation("Sauvegarde automatique terminée : {FileName}", result.FileName);
        }
        else
        {
            _logger.LogWarning("Sauvegarde automatique en échec : {Error}", result.Error);
        }
    }

    /// <summary>
    /// Détermine si une sauvegarde automatique est due. Si la dernière tentative a
    /// échoué, un délai court (FailureRetryInterval) est appliqué au lieu de la
    /// fréquence complète ; la sauvegarde suivante est alors tentée peu après.
    /// </summary>
    internal static bool IsDue(DateTime utcNow, DateTime? lastBackupAt, string? lastBackupStatus, int backupFrequencyMinutes)
    {
        if (!lastBackupAt.HasValue)
        {
            return true;
        }

        var elapsed = utcNow - lastBackupAt.Value;
        var interval = string.Equals(lastBackupStatus, "failed", StringComparison.OrdinalIgnoreCase)
            ? FailureRetryInterval
            : TimeSpan.FromMinutes(Math.Max(1, backupFrequencyMinutes));
        return elapsed >= interval;
    }
}
