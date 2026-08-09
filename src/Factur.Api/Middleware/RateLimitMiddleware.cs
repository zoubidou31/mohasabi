using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Factur.Api.Middleware;

/// <summary>
/// Middleware léger de limitation de débit (sliding window) pour les points sensibles
/// de l'API locale (envoi d'e-mail, mise à jour). Plutôt qu'une dépendance au package
/// AspNetCore.RateLimiting, il est autonome et sans configuration externe.
/// </summary>
public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitMiddleware> _logger;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private const int MaxRequests = 30;

    private static readonly ConcurrentDictionary<string, Queue<DateTime>> _requests = new();

    public RateLimitMiddleware(RequestDelegate next, ILogger<RateLimitMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Seulement sur les endpoints sensibles.
        if (IsSensitive(path, context.Request.Method))
        {
            var key = $"{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}:{path}";
            var now = DateTime.UtcNow;
            var rateLimited = false;

            var windowStart = now - Window;
            var queue = _requests.GetOrAdd(key, _ => new Queue<DateTime>());
            lock (queue)
            {
                while (queue.Count > 0 && queue.Peek() < windowStart)
                {
                    queue.Dequeue();
                }

                if (queue.Count >= MaxRequests)
                {
                    rateLimited = true;
                }
                else
                {
                    queue.Enqueue(now);
                }
            }

            if (rateLimited)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers.RetryAfter = "60";
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("""{"status":429,"message":"Trop de requêtes. Veuillez réessayer dans une minute."}""");
                return;
            }
        }

        await _next(context);
    }

    private static bool IsSensitive(string path, string method)
    {
        // Mettre à jour uniquement si l'exigence change : /api/update et /api/invoices/{id}/send-email.
        if (!path.StartsWith("/api/update", StringComparison.Ordinal) &&
            !path.StartsWith("/api/invoices/", StringComparison.Ordinal))
        {
            return false;
        }

        return (path.StartsWith("/api/update", StringComparison.Ordinal) && method == HttpMethods.Get)
            || (path.StartsWith("/api/update", StringComparison.Ordinal) && method == HttpMethods.Post)
            || path.EndsWith("/send-email", StringComparison.Ordinal);
    }
}

file static class HttpMethods
{
    public const string Get = "GET";
    public const string Post = "POST";
}
