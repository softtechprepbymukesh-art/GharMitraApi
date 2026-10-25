using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GharMitra.Api.Security;

/// <summary>
/// Maps domain exceptions to status codes and keeps internal detail out of
/// responses. Fixes Docs/GAP_ANALYSIS.md M7 (parameter names leaked to clients)
/// and makes a denied permission a 403 rather than a 500.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            UnauthorizedAccessException ex =>
                (StatusCodes.Status403Forbidden, "Forbidden", ex.Message),

            ArgumentException ex =>
                (StatusCodes.Status400BadRequest, "Invalid request", Clean(ex.Message)),

            InvalidOperationException ex =>
                (StatusCodes.Status409Conflict, "Conflict", ex.Message),

            _ => (StatusCodes.Status500InternalServerError, "Unexpected error",
                  _environment.IsDevelopment()
                      ? exception.Message
                      : "An unexpected error occurred. Please try again.")
        };

        if (status == StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Unhandled exception on {Path}", context.Request.Path);
        else
            _logger.LogInformation("{Status} on {Path}: {Detail}", status, context.Request.Path, detail);

        context.Response.StatusCode = status;

        await context.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Instance = context.Request.Path
            },
            cancellationToken);

        return true;
    }

    /// <summary>
    /// Strips the "(Parameter 'categoryId')" suffix the BCL appends, which
    /// exposes internal parameter names to callers.
    /// </summary>
    private static string Clean(string message)
    {
        var marker = message.IndexOf("(Parameter", StringComparison.Ordinal);
        return marker > 0 ? message[..marker].TrimEnd() : message;
    }
}
