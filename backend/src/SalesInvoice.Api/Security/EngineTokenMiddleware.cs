namespace SalesInvoice.Api.Security;

/// <summary>
/// Validates the X-Engine-Token header on all /internal/tools/* routes (Principle II).
/// Token is configured via the EngineToken environment variable — never sent to the client.
/// </summary>
public class EngineTokenMiddleware(RequestDelegate next, IConfiguration config)
{
    private const string HeaderName = "X-Engine-Token";

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/internal/tools"))
        {
            var expectedToken = config["EngineToken"];
            if (string.IsNullOrWhiteSpace(expectedToken))
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync("Engine token not configured.");
                return;
            }

            if (!context.Request.Headers.TryGetValue(HeaderName, out var providedToken)
                || providedToken != expectedToken)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Unauthorized.");
                return;
            }
        }

        await next(context);
    }
}
