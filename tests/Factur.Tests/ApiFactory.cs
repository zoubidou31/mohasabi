using Factur.Domain.Entities;
using Factur.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Factur.Tests;

/// <summary>
/// Héberge l'API sur une base SQLite temporaire et propre pour chaque exécution de test.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"factur-tests-{Guid.NewGuid():N}.db");

    public string DatabasePath => _dbPath;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Avec le hosting minimal (WebApplication), la ré-inscription directe du
        // DbContext est la méthode fiable pour isoler la base de tests.
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite($"Data Source={_dbPath}"));

            // La société est normalement saisie par l'utilisateur : les tests
            // d'intégration ont besoin d'une société par défaut pour exister.
            services.AddHostedService<TestSeedHostedService>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            File.Delete(_dbPath);
        }
        catch
        {
            // Les fichiers temporaires de test sont nettoyés au mieux.
        }
    }

    /// <summary>Sème une société par défaut dans la base de tests au démarrage de l'hôte.</summary>
    private sealed class TestSeedHostedService : IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public TestSeedHostedService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (!await context.Companies.AnyAsync(cancellationToken))
            {
                context.Companies.Add(new Company
                {
                    CompanyName = "Ma Société",
                    InvoicePrefix = "FAC",
                    ValidityDays = 30,
                });
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
