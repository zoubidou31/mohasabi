using Factur.Application.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Factur.Infrastructure.Services;

/// <summary>Déclenche les sauvegardes automatiques selon la fréquence configurée.</summary>
public class AutomaticBackupHostedService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

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
        var due = !state.LastBackupAt.HasValue
            || DateTime.UtcNow - state.LastBackupAt.Value >= TimeSpan.FromMinutes(settings.BackupFrequencyMinutes);

        if (!due)
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
}
