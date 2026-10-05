using Bizcord.Logging;
using ChatService.Application;
using ChatService.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.WebUtilities;

namespace ChatService.Infrastructure.Web;

/// <summary>Translates domain and application errors to ProblemDetails (RFC 9457). Other errors yield 500.</summary>
public sealed class DomainExceptionHandler(IProblemDetailsService problemDetails, ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // Order matters: subclasses before DomainException.
        int? status = exception switch
        {
            NotFoundException => StatusCodes.Status404NotFound,
            NotAllowedException => StatusCodes.Status403Forbidden,
            ConflictException => StatusCodes.Status409Conflict,
            DomainException => StatusCodes.Status400BadRequest,
            _ => null
        };

        var payload = new { ErrorType = exception.GetType().Name, Status = status ?? StatusCodes.Status500InternalServerError };

        if (status is null)
        {
            logger.Error("Unhandled exception.", payload, exception);
            return false;
        }

        // Client errors: expected and not an operational problem - Warning without stack trace.
        logger.Warning(exception.Message, payload);

        httpContext.Response.StatusCode = status.Value;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = status,
                Title = ReasonPhrases.GetReasonPhrase(status.Value),
                Detail = exception.Message
            }
        });
    }
}
