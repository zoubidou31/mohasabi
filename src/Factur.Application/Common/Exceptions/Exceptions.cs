namespace Factur.Application.Common.Exceptions;

/// <summary>Lever quand une ressource demandée n'existe pas.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

/// <summary>Lever quand une règle de gestion est violée (ex : facture finalisée non modifiable).</summary>
public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message) { }
}

/// <summary>
/// Lever quand l'état actuel d'une ressource rend l'opération impossible sans conflit
/// (ex : un client possédant des factures ne peut pas être supprimé).
/// </summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}

/// <summary>Lever quand la requête fournit une donnée invalide (ex : identifiant mal formé).</summary>
public class BadRequestException : Exception
{
    public BadRequestException(string message) : base(message) { }
}
