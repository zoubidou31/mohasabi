using System.Text.Json;
using Factur.Application.Common.Exceptions;
using FluentValidation;

namespace Factur.Api.Middleware;

/// <summary>Convertit les exceptions en réponses HTTP structurées et journalise les erreurs.</summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message) = exception switch
        {
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, exception.Message),
            NotFoundException or KeyNotFoundException => (StatusCodes.Status404NotFound, exception.Message),
            ConflictException => (StatusCodes.Status409Conflict, exception.Message),
            BadRequestException => (StatusCodes.Status400BadRequest, exception.Message),
            BusinessRuleException => (StatusCodes.Status400BadRequest, exception.Message),
            ValidationException => (StatusCodes.Status400BadRequest, "Données invalides. Vérifiez les champs du formulaire."),
            InvalidOperationException => (StatusCodes.Status400BadRequest, exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "Une erreur interne est survenue. Veuillez réessayer."),
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Erreur non gérée sur {Path}", context.Request.Path);
        }
        else
        {
            _logger.LogWarning(exception, "Erreur {Code} sur {Path} : {Message}", statusCode, context.Request.Path, exception.Message);
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var payload = new Dictionary<string, object?>
        {
            ["status"] = statusCode,
            ["message"] = message,
        };

        if (exception is ValidationException validation)
        {
            payload["errors"] = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
