using System.Diagnostics;
using LGBApp.Backend.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LGBApp.Backend.Middleware;

public static class ExceptionHandlingExtensions
{
    public const string ConcurrencyConflictMessage =
        "This item was updated by someone else. Refresh and try again.";

    public const string UniqueConflictMessage =
        "This action was already recorded.";

    /// <summary>
    /// Review #4 §2: map EF concurrency / unique conflicts to 409 instead of raw 500.
    /// Extracted for unit testing (tests use SQLite and never hit real Postgres races).
    /// </summary>
    public static (int StatusCode, string Message) MapException(Exception? exception)
    {
        if (exception is DomainException domain)
            return (domain.StatusCode, domain.Message);

        if (exception is DbUpdateConcurrencyException)
            return (StatusCodes.Status409Conflict, ConcurrencyConflictMessage);

        if (exception is DbUpdateException dbEx && IsUniqueViolation(dbEx))
            return (StatusCodes.Status409Conflict, UniqueConflictMessage);

        return (StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
    }

    public static bool IsUniqueViolation(DbUpdateException exception)
    {
        for (var e = (Exception?)exception; e != null; e = e.InnerException)
        {
            if (e is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
                return true;

            var msg = e.Message ?? string.Empty;
            if (msg.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("unique constraint", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("23505", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public static IApplicationBuilder UseLgbExceptionHandler(this IApplicationBuilder app)
    {
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var feature = context.Features.Get<IExceptionHandlerFeature>();
                var exception = feature?.Error;
                var logger = context.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("LgbExceptionHandler");

                var correlationId = Activity.Current?.Id ?? context.TraceIdentifier;
                var (statusCode, message) = MapException(exception);

                if (exception is DomainException domain)
                    logger.LogWarning(domain, "Domain exception {CorrelationId}: {Message}", correlationId, message);
                else if (statusCode >= 500 && exception != null)
                    logger.LogError(exception, "Unhandled exception {CorrelationId}", correlationId);
                else if (exception != null)
                    logger.LogWarning(exception, "Conflict mapped {CorrelationId}: {Message}", correlationId, message);

                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(new
                {
                    type = "about:blank",
                    title = statusCode >= 500 ? "Server Error" : "Request Error",
                    status = statusCode,
                    detail = message,
                    message,
                    correlationId,
                });
            });
        });

        return app;
    }
}
