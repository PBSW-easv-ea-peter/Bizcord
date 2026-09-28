using ChatService.Application;
using ChatService.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.WebUtilities;

namespace ChatService.Infrastructure.Web;

/// <summary>Oversætter domæne- og applikationsfejl til ProblemDetails (RFC 9457). Andre fejl giver 500.</summary>
public sealed class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // Rækkefølgen betyder noget: subklasser før DomainException.
        int? status = exception switch
        {
            NotFoundException => StatusCodes.Status404NotFound,
            NotAllowedException => StatusCodes.Status403Forbidden,
            ConflictException => StatusCodes.Status409Conflict,
            DomainException => StatusCodes.Status400BadRequest,
            _ => null
        };

        if (status is null)
            return false;

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
