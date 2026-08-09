using Factur.Application.DTOs;
using FluentValidation;

namespace Factur.Application.Validators;

/// <summary>
/// Règles communes (norme algérienne) :
/// NIF : 15 chiffres, NIS : 15 chiffres, RIB : 20 chiffres, RC : 16/00-0000000B00, ART : 13 chiffres.
/// </summary>
public static class FiscalValidationRules
{
    public static IRuleBuilderOptions<T, string> NIF<T>(this IRuleBuilder<T, string> rule) =>
        rule.Matches(@"^\d{15}$").WithMessage("Le NIF doit contenir exactement 15 chiffres.");

    public static IRuleBuilderOptions<T, string> NIS<T>(this IRuleBuilder<T, string> rule) =>
        rule.Matches(@"^\d{15}$").WithMessage("Le NIS doit contenir exactement 15 chiffres.");

    public static IRuleBuilderOptions<T, string> RC<T>(this IRuleBuilder<T, string> rule) =>
        rule.Matches(@"^\d{2}/\d{2}-\d{7}[A-Z]\d{2}$")
            .WithMessage("Format RC attendu : 16/00-0000000B00.");

    public static IRuleBuilderOptions<T, string> ART<T>(this IRuleBuilder<T, string> rule) =>
        rule.Matches(@"^\d{13}$").WithMessage("Le ART doit contenir exactement 13 chiffres.");

    public static IRuleBuilderOptions<T, string?> Phone<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Matches(@"^(?:0[567]\d{8}|\d{9})$")
            .WithMessage("Le téléphone doit être un numéro algérien valide (05/06/07 + 8 chiffres, ou 9 chiffres).");

    public static IRuleBuilderOptions<T, string?> Email<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Matches(@"^[^\s@]+@[^\s@]+\.(com|dz|net|org)$")
            .When(x => !string.IsNullOrWhiteSpace(x?.ToString()))
            .WithMessage("Adresse e-mail invalide (domaine doit être .com, .dz, .net ou .org).");

    public static IRuleBuilderOptions<T, string?> RIB<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Matches(@"^\d{20}$").When(x => !string.IsNullOrWhiteSpace(x?.ToString() ?? null))
            .WithMessage("Le RIB doit contenir exactement 20 chiffres.");

    public static IRuleBuilderOptions<T, string?> CCP<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Matches(@"^\d{6,12}$").When(x => !string.IsNullOrWhiteSpace(x?.ToString() ?? null))
            .WithMessage("Le CCP doit contenir entre 6 et 12 chiffres.");

    public static IRuleBuilderOptions<T, string?> PostalCode<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Matches(@"^\d{5}$").When(x => !string.IsNullOrWhiteSpace(x?.ToString() ?? null))
            .WithMessage("Le code postal doit contenir exactement 5 chiffres.");
}

public class UpdateCompanyRequestValidator : AbstractValidator<UpdateCompanyRequest>
{
    public UpdateCompanyRequestValidator()
    {
        RuleFor(x => x.CompanyName).NotEmpty().WithMessage("La raison sociale est obligatoire.")
            .MinimumLength(2).WithMessage("La raison sociale doit contenir au moins 2 caractères.");
        RuleFor(x => x.Address).NotEmpty().WithMessage("L'adresse est obligatoire.")
            .MinimumLength(10).WithMessage("L'adresse doit contenir au moins 10 caractères.");
        RuleFor(x => x.Phone).NotEmpty().WithMessage("Le téléphone est obligatoire.")
            .Phone().WithMessage("Le téléphone doit être un numéro algérien valide (05/06/07 + 8 chiffres, ou 9 chiffres).");
        RuleFor(x => x.Mobile).Phone().When(x => !string.IsNullOrWhiteSpace(x.Mobile))
            .WithMessage("Le mobile doit être un numéro algérien valide (05/06/07 + 8 chiffres, ou 9 chiffres).");
        RuleFor(x => x.Email).NotEmpty().WithMessage("L'e-mail est obligatoire.")
            .EmailAddress().WithMessage("Adresse e-mail invalide.")
            .Matches(@"\.(com|dz|net|org)$").WithMessage("Le domaine doit se terminer par .com, .dz, .net ou .org.");
        RuleFor(x => x.NIF).NIF();
        RuleFor(x => x.NIS).NIS();
        RuleFor(x => x.RC).RC();
        RuleFor(x => x.ART).ART();
        RuleFor(x => x.RIB).RIB();
        RuleFor(x => x.CCP).CCP();
        RuleFor(x => x.PostalCode).PostalCode();
        RuleFor(x => x.ValidityDays).InclusiveBetween(0, 365).WithMessage("La validité doit être entre 0 et 365 jours.");
        RuleFor(x => x.InvoicePrefix).NotEmpty().WithMessage("Le préfixe de facturation est obligatoire.")
            .Matches(@"^[A-Z0-9\-]+$").WithMessage("Le préfixe ne doit contenir que des lettres majuscules, chiffres et tirets.");
        RuleFor(x => x.PaymentConditions).NotEmpty().WithMessage("Les conditions de paiement sont obligatoires.");
        RuleFor(x => x.Penalties).Matches(@"\d+(?:[.,]\d+)?\s*%").When(x => !string.IsNullOrWhiteSpace(x.Penalties))
            .WithMessage("Les pénalités doivent contenir un pourcentage (ex : 0.5% par mois).");
    }
}

public class CreateClientRequestValidator : AbstractValidator<CreateClientRequest>
{
    public CreateClientRequestValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().WithMessage("Le nom du client est obligatoire.");
        RuleFor(x => x.NIF).Matches(@"^\d{13,15}$").When(x => !string.IsNullOrWhiteSpace(x.NIF))
            .WithMessage("Le NIF doit être un nombre de 13 à 15 chiffres.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).WithMessage("Adresse e-mail invalide.");
        RuleFor(x => x.Type).IsInEnum().WithMessage("Type de client invalide.");
    }
}

public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Reference).NotEmpty().WithMessage("La référence est obligatoire.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Le nom du produit est obligatoire.");
        RuleFor(x => x.DefaultPrice).GreaterThanOrEqualTo(0).WithMessage("Le prix ne peut être négatif.");
        RuleFor(x => x.DefaultTVARate).IsInEnum().WithMessage("Taux de TVA invalide.");
    }
}

public class InvoiceLineRequestValidator : AbstractValidator<InvoiceLineRequest>
{
    public InvoiceLineRequestValidator()
    {
        RuleFor(x => x.Description).NotEmpty().WithMessage("La description de la ligne est obligatoire.");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("La quantité doit être supérieure à 0.");
        RuleFor(x => x.UnitPriceHT).GreaterThanOrEqualTo(0).WithMessage("Le prix unitaire ne peut être négatif.");
        RuleFor(x => x.TVARate).IsInEnum().WithMessage("Taux de TVA invalide.");
    }
}

public class CreateInvoiceRequestValidator : AbstractValidator<CreateInvoiceRequest>
{
    public CreateInvoiceRequestValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty().WithMessage("Le client est obligatoire.");
        RuleFor(x => x.InvoiceDate).NotEmpty().WithMessage("La date d'émission est obligatoire.");
        RuleFor(x => x.ValidityDays).GreaterThanOrEqualTo(0).WithMessage("La validité ne peut être négative.");
        RuleFor(x => x.InvoiceType).IsInEnum().WithMessage("Type de facture invalide.");
        RuleFor(x => x.PaymentMethod).IsInEnum().WithMessage("Mode de paiement invalide.");
        RuleFor(x => x.RemiseValue).GreaterThan(0).When(x => x.RemiseValue.HasValue)
            .WithMessage("La remise doit être positive.");
        RuleFor(x => x.FraisPort).GreaterThanOrEqualTo(0).When(x => x.FraisPort.HasValue)
            .WithMessage("Les frais de port ne peuvent être négatifs.");
        RuleFor(x => x.AutresFrais).GreaterThanOrEqualTo(0).When(x => x.AutresFrais.HasValue)
            .WithMessage("Les autres frais ne peuvent être négatifs.");
        RuleFor(x => x.Lines).NotEmpty().WithMessage("La facture doit contenir au moins une ligne.");
        RuleForEach(x => x.Lines).SetValidator(new InvoiceLineRequestValidator());
    }
}
