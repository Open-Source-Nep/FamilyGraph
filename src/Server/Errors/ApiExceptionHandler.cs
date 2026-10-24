using FamilyGraph.Application;
using FamilyGraph.Application.Graph;
using FamilyGraph.Application.Import;
using FamilyGraph.Business;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Server.Errors;

/// <summary>
/// Turns domain and application errors into ProblemDetails with their stable error code (D-010):
/// 403 not permitted, 404 not found, 409 conflict with the current state, 422 any other domain rule,
/// 503 a graph query that timed out (D-009). A refused import file also carries its <c>problems</c>
/// and <c>moreProblems</c> (D-033). Other exceptions are left to the default handler.
/// </summary>
internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code) = exception switch
        {
            NotPermittedException e => (StatusCodes.Status403Forbidden, e.Code),
            ConflictException e => (StatusCodes.Status409Conflict, e.Code),
            DomainException e => (StatusCodes.Status422UnprocessableEntity, e.Code),
            NotFoundException e => (StatusCodes.Status404NotFound, e.Code),
            GraphQueryTimeoutException e => (StatusCodes.Status503ServiceUnavailable, e.Code),
            _ => (0, ""),
        };
        if (status == 0)
        {
            return false;
        }

        var details = new ProblemDetails
        {
            Status = status,
            Title = exception.Message,
            Extensions = { ["code"] = code },
        };
        if (exception is InvalidImportFileException invalid)
        {
            details.Extensions["problems"] = invalid.Problems;
            details.Extensions["moreProblems"] = invalid.MoreProblems;
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = details,
        });
    }
}
