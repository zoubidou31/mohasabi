using Factur.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Factur.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<TVABreakdown> TVABreakdowns => Set<TVABreakdown>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<User>(e =>
        {
            e.HasIndex(x => x.Username).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
        });

        builder.Entity<Client>(e =>
        {
            e.HasIndex(x => x.DisplayName);
        });

        builder.Entity<Product>(e =>
        {
            e.HasIndex(x => x.Reference).IsUnique();
            e.HasIndex(x => x.Name);
            e.HasOne(x => x.CategoryRef).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Category>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
        });

        builder.Entity<Company>(e =>
        {
            e.Property(x => x.LogoPath).HasMaxLength(512);
            e.Property(x => x.StampPath).HasMaxLength(512);
        });

        builder.Entity<Invoice>(e =>
        {
            e.HasIndex(x => x.InvoiceNumber).IsUnique();
            e.HasIndex(x => x.InvoiceDate);
            e.HasIndex(x => x.Status);
            // Index composites (coût de pagination / rapports) :
            // - ordre de tri par défaut (date + séquence) et MAX(Séquence) du mois ;
            // - filtre « impayés » (statuts exclus + date d'échéance) ;
            // - rapports mensuels (date + statut).
            e.HasIndex(x => new { x.InvoiceDate, x.Sequence });
            e.HasIndex(x => new { x.DueDate, x.Status });
            e.HasIndex(x => new { x.InvoiceDate, x.Status });
            e.Property(x => x.InvoiceNumber).HasMaxLength(64);
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.TVABreakdowns).WithOne().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Payments).WithOne().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<InvoiceLine>(e =>
        {
            e.Property(x => x.TVARate).HasConversion<int>();
            e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<TVABreakdown>(e =>
        {
            e.Property(x => x.TVARate).HasConversion<int>();
            e.HasOne(x => x.Invoice).WithMany(i => i.TVABreakdowns).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Payment>(e =>
        {
            e.Property(x => x.PaymentMethod).HasConversion<int>();
            e.HasOne(x => x.Invoice).WithMany(i => i.Payments).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<User>(e =>
        {
            e.Property(x => x.Role).HasConversion<int>();
        });

        builder.Entity<Client>(e =>
        {
            e.Property(x => x.Type).HasConversion<int>();
        });

        builder.Entity<AuditLog>(e =>
        {
            e.HasIndex(x => x.Timestamp);
            e.Property(x => x.EntityType).HasMaxLength(64);
            e.Property(x => x.EntityId).HasMaxLength(64);
        });
    }
}
