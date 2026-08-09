using Factur.Application.Common.Exceptions;
using Factur.Application.Common.Mapping;
using Factur.Application.DTOs;
using Factur.Application.Interfaces;
using Factur.Domain.Entities;
using Factur.Domain.Enums;
using Factur.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Factur.Infrastructure.Services;

public class CompanyService : ICompanyService
{
    private readonly ApplicationDbContext _context;
    private readonly IValidator<UpdateCompanyRequest> _validator;
    private readonly IAuditLogger _auditLogger;
    private readonly IOptions<StorageOptions> _storage;

    public CompanyService(
        ApplicationDbContext context,
        IValidator<UpdateCompanyRequest> validator,
        IAuditLogger auditLogger,
        IOptions<StorageOptions> storage)
    {
        _context = context;
        _validator = validator;
        _auditLogger = auditLogger;
        _storage = storage;
    }

    public async Task<CompanyDto> GetAsync(CancellationToken ct = default)
    {
        var company = await _context.Companies.OrderBy(c => c.CreatedDate).FirstOrDefaultAsync(ct);
        return (company ?? new Company()).ToDto();
    }

    public async Task<CompanyDto> SaveAsync(UpdateCompanyRequest request, CancellationToken ct = default)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        var company = await _context.Companies.OrderBy(c => c.CreatedDate).FirstOrDefaultAsync(ct);
        var isNew = company is null;
        company ??= new Company();

        company.CompanyName = request.CompanyName.Trim();
        company.Address = request.Address.Trim();
        company.PostalCode = request.PostalCode;
        company.City = request.City;
        company.Wilaya = request.Wilaya;
        company.Phone = request.Phone.Trim();
        company.Mobile = request.Mobile;
        company.Email = request.Email.Trim();
        company.NIF = request.NIF?.Trim() ?? string.Empty;
        company.NIS = request.NIS?.Trim() ?? string.Empty;
        company.RC = request.RC?.Trim() ?? string.Empty;
        company.ART = request.ART?.Trim() ?? string.Empty;
        company.RIB = request.RIB;
        company.CCP = request.CCP;
        company.BankName = request.BankName;
        company.InvoicePrefix = request.InvoicePrefix?.Trim() ?? "FAC";
        company.InvoiceSerie = request.InvoiceSerie ?? string.Empty;
        company.ValidityDays = request.ValidityDays;
        company.DefaultTVARate = request.DefaultTVARate;
        company.PaymentConditions = request.PaymentConditions;
        company.Penalties = request.Penalties;
        company.BankAccountNumber = request.BankAccountNumber;
        company.UseBankersRounding = request.UseBankersRounding;

        // Logo (base64) et tampon stockés sur disque
        if (!string.IsNullOrWhiteSpace(request.LogoData))
        {
            company.LogoPath = await SaveImageAsync(request.LogoData, "logo", ct);
        }

        if (!string.IsNullOrWhiteSpace(request.StampData))
        {
            company.StampPath = await SaveImageAsync(request.StampData, "tampon", ct);
        }

        if (isNew)
        {
            _context.Companies.Add(company);
        }
        else
        {
            company.UpdatedDate = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Company", company.Id.ToString(), isNew ? "Création" : "Modification", ct: ct);

        return company.ToDto();
    }

    private async Task<string?> SaveImageAsync(string dataUrl, string name, CancellationToken ct)
    {
        var parts = dataUrl.Split(',');
        if (parts.Length != 2)
        {
             throw new BadRequestException("Données d'image invalides.");
        }

        var mimeType = parts[0].Split(':')[1].Split(';')[0];
        var extension = mimeType switch
        {
            "image/png" => "png",
            "image/jpeg" or "image/jpg" => "jpg",
            _ => throw new BadRequestException("Type d'image non supporté : uniquement PNG et JPEG."),
        };

        var bytes = Convert.FromBase64String(parts[1]);

        // Limite de taille pour éviter un déni de service par upload volumineux.
        const int maxBytes = 2 * 1024 * 1024;
        if (bytes.Length > maxBytes)
        {
            throw new BadRequestException("L'image est trop volumineuse (maximum 2 Mo).");
        }

        // Vérifie les magic bytes pour s'assurer que le contenu correspond au type déclaré.
        ReadOnlySpan<byte> pngSig = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (mimeType == "image/png" && !bytes.AsSpan().StartsWith(pngSig))
        {
            throw new BadRequestException("Le contenu du fichier image est invalide (signature PNG incorrecte).");
        }

        ReadOnlySpan<byte> jpegSig = [0xFF, 0xD8, 0xFF];
        if ((mimeType == "image/jpeg" || mimeType == "image/jpg") && !bytes.AsSpan().StartsWith(jpegSig))
        {
            throw new BadRequestException("Le contenu du fichier image est invalide (signature JPEG incorrecte).");
        }

        var uploadsDir = StoragePaths.ResolveUploads(_storage.Value);
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"{name}-{Guid.NewGuid():N}.{extension}";
        var fullPath = Path.Combine(uploadsDir, fileName);
        await File.WriteAllBytesAsync(fullPath, bytes, ct);
        return $"uploads/{fileName}";
    }
}
