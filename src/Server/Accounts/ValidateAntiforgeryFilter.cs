using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Server.Accounts;

/// <summary>
/// Rejects state-changing API requests without a valid antiforgery token (D-011). The Client sends
/// the token from the <c>XSRF-TOKEN</c> cookie in the <c>X-XSRF-TOKEN</c> header. MVC's own
/// filter needs the view features, which this API does not use.
/// </summary>
internal sealed class ValidateAntiforgeryFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
        {
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "The request is missing a valid antiforgery token.",
                Extensions = { ["code"] = "request.antiforgery_invalid" },
            })
            { StatusCode = StatusCodes.Status400BadRequest };
        }
    }
}
