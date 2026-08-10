using Factur.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Factur.Infrastructure.Services;

/// <summary>Marqueurs de cycle de vie : arrêt propre, redémarrage attendu, session précédente interrompue.</summary>
public class AppStatusService : IAppStatusService
{
    private readonly string _root;
    private readonly string _cleanExitMarker;
    private readonly string _restartPendingMarker;
    private bool _restartPending;

    public AppStatusService(IConfiguration configuration)
    {
        _root = AppPaths.ResolveRoot(configuration["App:DataRoot"], configuration.GetConnectionString("DefaultConnection") ?? string.Empty);
        _cleanExitMarker = AppPaths.CleanExitMarker(_root);
        _restartPendingMarker = AppPaths.RestartPendingMarker(_root);
        _restartPending = File.Exists(_restartPendingMarker);
    }

    /// <summary>Vrai si la session précédente ne s'est pas fermée normalement.</summary>
    public bool UncleanExitDetected { get; private set; }

    public void MarkCleanExit()
    {
        try
        {
            Directory.CreateDirectory(_root);
            File.WriteAllText(_cleanExitMarker, DateTime.UtcNow.ToString("O"));
        }
        catch
        {
            // Non bloquant.
        }
    }

    public bool IsRestartPending => _restartPending;

    public void SetRestartPending(bool value)
    {
        _restartPending = value;
        try
        {
            Directory.CreateDirectory(_root);
            if (value)
            {
                File.WriteAllText(_restartPendingMarker, DateTime.UtcNow.ToString("O"));
            }
            else if (File.Exists(_restartPendingMarker))
            {
                File.Delete(_restartPendingMarker);
            }
        }
        catch
        {
            // Non bloquant.
        }
    }

    /// <summary>Évalue l'état de la session précédente (appelé une fois au démarrage de l'API).</summary>
    public void EvaluateAtStartup()
    {
        // Un redémarrage attendu (restauration) explique l'absence de marqueur d'arrêt propre.
        if (_restartPending)
        {
            _restartPending = false;
            UncleanExitDetected = false;
            try
            {
                if (File.Exists(_restartPendingMarker))
                {
                    File.Delete(_restartPendingMarker);
                }
            }
            catch { }
            return;
        }

        if (File.Exists(_cleanExitMarker))
        {
            UncleanExitDetected = false;
            try
            {
                File.Delete(_cleanExitMarker);
            }
            catch { }
        }
        else
        {
            UncleanExitDetected = true;
        }
    }

    public Task<bool> HasUncleanExitAsync(CancellationToken ct = default)
        => Task.FromResult(UncleanExitDetected);
}
