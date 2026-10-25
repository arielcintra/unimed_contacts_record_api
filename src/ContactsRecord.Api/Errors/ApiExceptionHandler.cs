using ContactsRecord.Application.Common.Exceptions;
using ContactsRecord.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace ContactsRecord.Api.Errors;

/// <summary>Translates expected exceptions into ProblemDetails (RFC 9457) responses.</summary>
internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        int? status = exception switch
        {
            DomainException => StatusCodes.Status400BadRequest,
            UnauthorizedException => StatusCodes.Status401Unauthorized,
            NotFoundException => StatusCodes.Status404NotFound,
            ConflictException => StatusCodes.Status409Conflict,
            ServiceUnavailableException => StatusCodes.Status503ServiceUnavailable,
            _ => null,
        };

        // Unexpected errors fall through to the default 500 response.
        if (status is null) return false;

        httpContext.Response.StatusCode = status.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Status = status, Detail = exception.Message },
        });
    }
}
