using Bizcord.Logging;
using ChatService.Application;
using ChatService.Controllers;
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
            DatabaseUnavailableException => StatusCodes.Status503ServiceUnavailable,
            _ => null
        };

        var payload = new
        {
            ErrorType = exception.GetType().Name,
            Status = status ?? StatusCodes.Status500InternalServerError,
            // Which chat/message/user was affected - ids only (GDPR). Null when the endpoint has none.
            ChatId = RouteId(httpContext, "chatId"),
            MessageId = RouteId(httpContext, "messageId"),
            UserId = UserId(httpContext)
        };

        if (status is null)
        {
            logger.Error("Unhandled exception.", payload, exception);
            return false;
        }

        if (exception is DatabaseUnavailableException { RetryAfter: { } retryAfter })
        {
            // Circuit open: the database was not called. Warning - one Error per rejected request would flood the log.
            logger.Warning(exception.Message, payload);
            httpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        }
        else if (exception is DatabaseUnavailableException)
        {
            // The database timed out or is unreachable - an operational problem.
            logger.Error(exception.Message, payload, exception);
        }
        else
        {
            // Client errors: expected and not an operational problem - Warning without stack trace.
            logger.Warning(exception.Message, payload);
        }

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

    /// <summary>UseExceptionHandler clears the request's route values before the handlers run - the feature keeps a copy.</summary>
    private static Guid? RouteId(HttpContext httpContext, string name) =>
        Guid.TryParse(httpContext.Features.Get<IExceptionHandlerFeature>()?.RouteValues?[name] as string, out var id) ? id : null;

    /// <summary>Parsed, so a malformed header can't put arbitrary text in the log.</summary>
    private static Guid? UserId(HttpContext httpContext) =>
        Guid.TryParse(httpContext.Request.Headers[UserHeader.Name].ToString(), out var id) ? id : null;
}
