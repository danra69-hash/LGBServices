using System.Diagnostics;
using LGBApp.Backend.Services;
using Microsoft.AspNetCore.Diagnostics;

namespace LGBApp.Backend.Middleware;

public static class ExceptionHandlingExtensions
{
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
                var statusCode = StatusCodes.Status500InternalServerError;
                var message = "An unexpected error occurred.";

                if (exception is DomainException domain)
                {
                    statusCode = domain.StatusCode;
                    message = domain.Message;
                    logger.LogWarning(domain, "Domain exception {CorrelationId}: {Message}", correlationId, message);
                }
                else if (exception != null)
                {
                    logger.LogError(exception, "Unhandled exception {CorrelationId}", correlationId);
                }

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
