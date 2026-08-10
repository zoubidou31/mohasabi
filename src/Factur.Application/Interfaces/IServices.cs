using Factur.Application.DTOs;

namespace Factur.Application.Interfaces;

/// <summary>Gestion des informations du vendeur (société).</summary>
public interface ICompanyService
{
    Task<CompanyDto> GetAsync(CancellationToken ct = default);
    Task<CompanyDto> SaveAsync(UpdateCompanyRequest request, CancellationToken ct = default);
}

/// <summary>Gestion des clients.</summary>
public interface IClientService
{
    Task<Guid> CreateAsync(CreateClientRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateClientRequest request, CancellationToken ct = default);
    Task ArchiveAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<ClientDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<ClientDto>> GetPagedAsync(ClientQuery query, CancellationToken ct = default);
    Task<ClientStatsDto> GetStatsAsync(Guid id, CancellationToken ct = default);
    Task<int> ImportAsync(IEnumerable<CreateClientRequest> clients, CancellationToken ct = default);
}

/// <summary>Gestion des catégories de produits.</summary>
public interface ICategoryService
{
    Task<Guid> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<CategoryDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(bool? active = null, CancellationToken ct = default);
    Task<int> GetProductCountAsync(Guid categoryId, CancellationToken ct = default);
}

/// <summary>Gestion des produits et services.</summary>
public interface IProductService
{
    Task<Guid> CreateAsync(CreateProductRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<ProductDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<ProductDto>> GetPagedAsync(string? search = null, bool includeInactive = false, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct = default);
    Task<int> ImportAsync(IEnumerable<CreateProductRequest> products, CancellationToken ct = default);
}

/// <summary>Gestion des factures (CRUD, cycle de vie, exports).</summary>
public interface IInvoiceService
{
    Task<InvoiceDto> CreateAsync(CreateInvoiceRequest request, CancellationToken ct = default);
    Task<InvoiceDto> UpdateAsync(Guid id, UpdateInvoiceRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<InvoiceDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<InvoiceSummaryDto>> GetPagedAsync(InvoiceQuery query, CancellationToken ct = default);
    Task<InvoiceDto> FinalizeAsync(Guid id, CancellationToken ct = default);
    Task<InvoiceDto> MarkPaidAsync(Guid id, MarkPaidRequest request, CancellationToken ct = default);
    Task<InvoiceDto> CancelAsync(Guid id, string? reason, CancellationToken ct = default);
    Task<InvoiceDto> DuplicateAsync(Guid id, CancellationToken ct = default);
    Task<InvoiceDto> CreateCreditNoteAsync(Guid id, CancellationToken ct = default);
    Task<string> GetNextNumberAsync(DateTime? date = null, CancellationToken ct = default);
    Task RegisterPaymentAsync(Guid invoiceId, PaymentRequest request, CancellationToken ct = default);
    Task DeletePaymentAsync(Guid invoiceId, Guid paymentId, CancellationToken ct = default);
    Task<int> ImportLinesAsync(Guid invoiceId, IEnumerable<ImportLineRequest> lines, CancellationToken ct = default);
}

/// <summary>Rapports et statistiques.</summary>
public interface IReportService
{
    Task<MonthlyReportDto> GetMonthlyReportAsync(int year, int month, CancellationToken ct = default);
    Task<TVAReportDto> GetTVAReportAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<IReadOnlyList<InvoiceSummaryDto>> GetUnpaidInvoicesAsync(DateTime? asOf = null, CancellationToken ct = default);
    Task<IReadOnlyList<TopClientDto>> GetTopClientsAsync(int count = 10, DateTime? from = null, DateTime? to = null, CancellationToken ct = default);
    Task<IReadOnlyList<YearlyPointDto>> GetYearlyTotalsAsync(int year, CancellationToken ct = default);
}

/// <summary>Historique des modifications (audit trail).</summary>
public interface IAuditService
{
    Task<IReadOnlyList<AuditLogDto>> GetAsync(string? entityType = null, DateTime? from = null, DateTime? to = null, int limit = 200, CancellationToken ct = default);
}

/// <summary>Export des factures et des rapports en PDF, Excel, Word, CSV.</summary>
public interface IExportService
{
    Task<byte[]> ExportPdfAsync(Guid invoiceId, string? lang = null, CancellationToken ct = default);
    Task<byte[]> ExportExcelAsync(Guid invoiceId, string? lang = null, CancellationToken ct = default);
    Task<byte[]> ExportWordAsync(Guid invoiceId, string? lang = null, CancellationToken ct = default);
    byte[] ExportCsv(InvoiceDto invoice, string? lang = null);
    Task<byte[]> ExportInvoicesExcelAsync(IEnumerable<InvoiceSummaryDto> invoices, string? lang = null, CancellationToken ct = default);

    Task<byte[]> ExportMonthlyReportPdfAsync(int year, int month, string? lang = null, CancellationToken ct = default);
    Task<byte[]> ExportMonthlyReportExcelAsync(int year, int month, string? lang = null, CancellationToken ct = default);
    Task<byte[]> ExportTvaReportPdfAsync(DateTime? from, DateTime? to, string? lang = null, CancellationToken ct = default);
    Task<byte[]> ExportTvaReportExcelAsync(DateTime? from, DateTime? to, string? lang = null, CancellationToken ct = default);
    Task<byte[]> ExportUnpaidPdfAsync(string? lang = null, CancellationToken ct = default);
    Task<byte[]> ExportUnpaidExcelAsync(string? lang = null, CancellationToken ct = default);
    Task<byte[]> ExportTopClientsPdfAsync(int count = 10, string? lang = null, CancellationToken ct = default);
    Task<byte[]> ExportTopClientsExcelAsync(int count = 10, string? lang = null, CancellationToken ct = default);
}

/// <summary>Envoi d'e-mails.</summary>
public interface IEmailService
{
    Task SendInvoiceAsync(Guid invoiceId, string toAddress, string? message, CancellationToken ct = default);
}

/// <summary>Vérification et téléchargement des mises à jour.</summary>
public interface IUpdateService
{
    string CurrentVersion { get; }
    Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default);
    Task<string> DownloadInstallerAsync(string downloadUrl, string? expectedSha256 = null, CancellationToken ct = default);
}

/// <summary>Préférences générales persistées (page Options).</summary>
public interface ISettingsService
{
    Task<AppSettings> GetAsync(CancellationToken ct = default);
    Task<AppSettings> SaveAsync(AppSettings settings, CancellationToken ct = default);

    /// <summary>
    /// Persiste les préférences courantes (défauts) si le fichier n'existe pas encore.
    /// Garantit qu'une sauvegarde de première exécution peut toujours inclure settings.json.
    /// </summary>
    Task EnsurePersistedAsync(CancellationToken ct = default);

    Task<BackupState> GetBackupStateAsync(CancellationToken ct = default);
    Task SetBackupStateAsync(BackupState state, CancellationToken ct = default);
}

/// <summary>Sauvegarde et restauration des données.</summary>
public interface IBackupService
{
    Task<BackupRunResult> CreateAsync(CancellationToken ct = default);
    Task<IReadOnlyList<BackupInfo>> ListAsync(CancellationToken ct = default);
    Task<BackupStatusDto> GetStatusAsync(CancellationToken ct = default);
    Task DeleteAsync(string fileName, CancellationToken ct = default);
}

/// <summary>Restauration sécurisée d'une sauvegarde.</summary>
public interface IRestoreService
{
    Task<RestoreResult> RestoreAsync(RestoreRequest request, CancellationToken ct = default);

    /// <summary>Applique une restauration en attente (appelé au démarrage, avant l'ouverture de la base).</summary>
    Task ApplyPendingAsync(CancellationToken ct = default);
}

/// <summary>Marqueurs de cycle de vie de l'application (arrêt propre, redémarrage).</summary>
public interface IAppStatusService
{
    void MarkCleanExit();
    bool IsRestartPending { get; }
    void SetRestartPending(bool value);

    /// <summary>Vrai si la session précédente ne s'est pas fermée normalement.</summary>
    bool UncleanExitDetected { get; }

    /// <summary>Évalue l'état de la session précédente (appelé une fois au démarrage de l'API).</summary>
    void EvaluateAtStartup();

    Task<bool> HasUncleanExitAsync(CancellationToken ct = default);
}
