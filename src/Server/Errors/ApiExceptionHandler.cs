using FamilyGraph.Application;
using FamilyGraph.Business;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Server.Errors;

/// <summary>
/// Turns domain and application errors into ProblemDetails with their stable error code (D-010):
/// 403 not permitted, 404 not found, 422 any other domain rule. Other exceptions are left to the
/// default handler.
/// </summary>
internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code) = exception switch
        {
            NotPermittedException e => (StatusCodes.Status403Forbidden, e.Code),
            DomainException e => (StatusCodes.Status422UnprocessableEntity, e.Code),
            NotFoundException e => (StatusCodes.Status404NotFound, e.Code),
            _ => (0, ""),
        };
        if (status == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = exception.Message,
                Extensions = { ["code"] = code },
            },
        });
    }
}
