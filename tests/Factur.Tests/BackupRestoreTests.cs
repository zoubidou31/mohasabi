using System.IO.Compression;
using System.Net.Http.Json;
using Factur.Application.DTOs;
using Factur.Application.Interfaces;
using Factur.Infrastructure.Persistence;
using Factur.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Factur.Tests;

/// <summary>
/// Héberge l'API sur une racine de données entièrement vierge et isolée
/// (base SQLite, settings.json, Backups, uploads) pour chaque exécution.
/// </summary>
public sealed class BackupRestoreFactory : WebApplicationFactory<Program>
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"factur-backup-{Guid.NewGuid():N}");
    private readonly string _dbPath;
    private readonly string _uploadsPath;

    public string Root => _root;

    public BackupRestoreFactory()
    {
        _dbPath = Path.Combine(_root, "data", "mohasabi.db");
        _uploadsPath = Path.Combine(_root, "uploads");
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:DataRoot"] = _root,
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_dbPath}",
                ["Storage:UploadsPath"] = _uploadsPath,
            });
        });

        builder.ConfigureServices(services =>
        {
            // La sauvegarde automatique planifiée est désactivée dans les tests :
            // les scénarios sont déclenchés explicitement pour rester déterministes.
            var hosted = services.SingleOrDefault(d => d.ImplementationType == typeof(AutomaticBackupHostedService));
            if (hosted is not null)
            {
                services.Remove(hosted);
            }

            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite($"Data Source={_dbPath}"));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
            // Nettoyage au mieux des fichiers temporaires de test.
        }
    }
}

/// <summary>Régression du bug de première exécution de la sauvegarde automatique.</summary>
public class BackupRestoreTests
{
    [Fact]
    public async Task PremiereSauvegarde_SurDossierPristine_ReussitEtArchiveComplete()
    {
        using var factory = new BackupRestoreFactory();
        var client = factory.CreateClient();

        // Répertoire de données vierge : settings.json n'existe pas encore.
        var settingsFile = Path.Combine(factory.Root, "settings.json");
        Assert.False(File.Exists(settingsFile));

        var result = await PostBackupNowAsync(client);
        Assert.True(result.Success, result.Error);

        // La sauvegarde persiste les préférences par défaut (settings.json) avant archivage.
        Assert.True(File.Exists(settingsFile), "settings.json doit exister après la première sauvegarde.");

        // Une seule sauvegarde, complète et vérifiable (mohasabi.db + settings.json).
        var backupsDir = Path.Combine(factory.Root, "Backups");
        var zipFiles = Directory.GetFiles(backupsDir, "*.zip");
        Assert.Single(zipFiles);
        using (var archive = ZipFile.OpenRead(zipFiles[0]))
        {
            var names = archive.Entries.Select(e => e.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.Contains("mohasabi.db", names);
            Assert.Contains("settings.json", names);
        }

        // Statut de sauvegarde : succès, pas d'état "Protégée" sans archive valide.
        var status = await client.GetFromJsonAsync<BackupStatusDto>("/api/backup/status");
        Assert.NotNull(status);
        Assert.Equal("ok", status!.LastBackupStatus);
        Assert.Equal(1, status.BackupCount);
        Assert.True(status.TotalSize > 0);
    }

    [Fact]
    public async Task EchecSauvegarde_NeLaissePasDOrphelinNiSucces()
    {
        using var factory = new BackupRestoreFactory();
        var client = factory.CreateClient();

        // Force un échec après la création du ZIP : le chemin index.json est occupé
        // par un dossier, la réinscription de l'index échoue (IOException).
        var backupsDir = Path.Combine(factory.Root, "Backups");
        Directory.CreateDirectory(backupsDir);
        Directory.CreateDirectory(Path.Combine(backupsDir, "index.json"));

        var result = await PostBackupNowAsync(client);
        Assert.False(result.Success);

        // Aucun ZIP orphelin ne doit subsister après l'échec.
        Assert.Empty(Directory.GetFiles(backupsDir, "*.zip"));

        // Statut : échec, aucun succès enregistré, aucune sauvegarde indexée.
        var status = await client.GetFromJsonAsync<BackupStatusDto>("/api/backup/status");
        Assert.NotNull(status);
        Assert.Equal("failed", status!.LastBackupStatus);
        Assert.Equal(0, status.BackupCount);
        Assert.Equal(0, status.TotalSize);
    }

    [Fact]
    public async Task Restauration_DeLaPremiereSauvegarde_Fonctionne()
    {
        using var factory = new BackupRestoreFactory();
        var client = factory.CreateClient();

        // 1. Première sauvegarde sur répertoire vierge.
        var backup = await PostBackupNowAsync(client);
        Assert.True(backup.Success, backup.Error);

        // 2. Données ajoutées après la sauvegarde.
        var create = await client.PostAsJsonAsync("/api/clients", new { displayName = "Client après sauvegarde", type = "Entreprise", nif = "099916000000013", phone = "0550123456", address = "Cité 20 Août 1956, Alger" });
        create.EnsureSuccessStatusCode();
        Assert.Equal(1, await GetClientCountAsync(client));

        // 3. Demande de restauration : prête, exige un redémarrage.
        var restoreResponse = await client.PostAsJsonAsync("/api/restore", new { fileName = backup.FileName });
        restoreResponse.EnsureSuccessStatusCode();
        var restore = await restoreResponse.Content.ReadFromJsonAsync<RestoreResult>();
        Assert.NotNull(restore);
        Assert.True(restore!.Success);
        Assert.True(restore.RequiresRestart);

        // 4. Application de la restauration en attente (simule le démarrage de l'API,
        //    où la restauration est appliquée avant toute connexion à la base).
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        var restoreService = factory.Services.GetRequiredService<IRestoreService>();
        await restoreService.ApplyPendingAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        // 5. Les données postérieures à la sauvegarde ont disparu (base restaurée).
        Assert.Equal(0, await GetClientCountAsync(client));

        // 6. Les préférences restaurées correspondent au contenu de la sauvegarde (défauts).
        var settings = await client.GetFromJsonAsync<AppSettings>("/api/settings");
        Assert.NotNull(settings);
        Assert.Equal("fr", settings!.Language);
        Assert.True(settings.SplashEnabled);
    }

    // ---------------------------------------------------------------- helpers

    private static async Task<BackupRunResult> PostBackupNowAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/backup/now", null);
        return await response.Content.ReadFromJsonAsync<BackupRunResult>() ?? new BackupRunResult { Success = false };
    }

    private static async Task<int> GetClientCountAsync(HttpClient client)
    {
        var paged = await client.GetFromJsonAsync<PagedClientResult>("/api/clients");
        return paged?.TotalCount ?? 0;
    }

    private sealed record PagedClientResult(int TotalCount);
}

/// <summary>Logique de réessai de la sauvegarde automatique après un échec.</summary>
public class AutomaticBackupRetryTests
{
    private static readonly DateTime Now = new(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void JamaisSauvegarde_EstDue()
        => Assert.True(AutomaticBackupHostedService.IsDue(Now, null, null, 30));

    [Fact]
    public void DernierSucces_AvantIntervalle_PasDue()
        => Assert.False(AutomaticBackupHostedService.IsDue(Now, Now.AddMinutes(-10), "ok", 30));

    [Fact]
    public void DernierSucces_IntervalleEcoule_Due()
        => Assert.True(AutomaticBackupHostedService.IsDue(Now, Now.AddMinutes(-31), "ok", 30));

    [Fact]
    public void DernierEchec_PendantDureeRetry_PasEncoreDue()
        => Assert.False(AutomaticBackupHostedService.IsDue(Now, Now.AddMinutes(-2), "failed", 1440));

    [Fact]
    public void DernierEchec_ApresDureeRetry_DueSansAttendreLaFrequence()
        => Assert.True(AutomaticBackupHostedService.IsDue(Now, Now.AddMinutes(-6), "failed", 1440));
}
