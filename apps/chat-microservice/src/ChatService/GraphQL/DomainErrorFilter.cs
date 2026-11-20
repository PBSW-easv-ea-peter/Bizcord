using Bizcord.Logging;
using ChatService.Application;
using ChatService.Domain;
using HotChocolate.Execution;

namespace ChatService.GraphQL;

/// <summary>
/// GraphQL counterpart of DomainExceptionHandler: Known domain errors get a code and their message,
/// everything else stays "Unexpected Execution Error" so internals don't leak.
/// </summary>
public sealed class DomainErrorFilter(ILogger<DomainErrorFilter> logger) : IErrorFilter
{
    public IError OnError(IError error)
    {
        // Order matters: subclasses before DomainException.
        var code = error.Exception switch
        {
            NotFoundException => "NOT_FOUND",
            NotAllowedException => "FORBIDDEN",
            ConflictException => "CONFLICT",
            DomainException => "BAD_REQUEST",
            _ => null
        };

        if (code is null)
            return error;

        // Client errors: expected, so Warning without stack trace - same as REST.
        logger.Warning(
            error.Exception!.Message, 
            new { 
                ErrorType = error.Exception.GetType()
                                .Name, Code = code});
        
        return error
            .WithMessage(error.Exception.Message)
            .WithCode(code);
    }
}
