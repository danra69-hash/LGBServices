using System.Security.Claims;

namespace LGBApp.Backend.Middleware;

public class MustChangePasswordMiddleware
{
    private static readonly string[] AllowedPaths =
    [
        "/api/auth/login",
        "/api/auth/register",
        "/api/auth/change-password",
    ];

    private readonly RequestDelegate _next;

    public MustChangePasswordMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && string.Equals(context.User.FindFirstValue("must_change_password"), "true", StringComparison.OrdinalIgnoreCase))
        {
            var path = context.Request.Path.Value ?? string.Empty;
            if (!AllowedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "You must change your password before continuing.",
                    code = "PASSWORD_CHANGE_REQUIRED",
                });
                return;
            }
        }

        await _next(context);
    }
}
