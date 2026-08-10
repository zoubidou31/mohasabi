using Factur.Application.Common.Interfaces;
using Factur.Application.Interfaces;
using Factur.Infrastructure.Persistence;
using Factur.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Factur.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? "Data Source=mohasabi.db";
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite(connectionString));

        services.Configure<EmailSettings>(configuration.GetSection("Email"));
        services.Configure<StorageOptions>(configuration.GetSection("Storage"));
        services.Configure<UpdateOptions>(configuration.GetSection("Update"));
        services.Configure<AppOptions>(configuration.GetSection("App"));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IExportService, ExportService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IUpdateService, UpdateService>();

        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IRestoreService, RestoreService>();
        services.AddSingleton<IAppStatusService, AppStatusService>();
        services.AddHostedService<AutomaticBackupHostedService>();

        return services;
    }

    /// <summary>Applique une restauration en attente, puis évalue l'arrêt de la session précédente.</summary>
    public static async Task InitializeAppStateAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var restoreService = scope.ServiceProvider.GetRequiredService<IRestoreService>();
        await restoreService.ApplyPendingAsync();

        var statusService = scope.ServiceProvider.GetRequiredService<IAppStatusService>();
        statusService.EvaluateAtStartup();
    }

    /// <summary>Applique les migrations de la base de données.</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();
    }
}
