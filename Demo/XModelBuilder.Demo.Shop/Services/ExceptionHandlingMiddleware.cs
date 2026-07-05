using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace XModelBuilder.Demo.Shop.Services;

/// <summary>Maps <see cref="DomainException"/>s onto ProblemDetails responses with the right status code.</summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DomainException ex)
        {
            var status = ex switch
            {
                NotFoundException => HttpStatusCode.NotFound,
                ForbiddenException => HttpStatusCode.Forbidden,
                BusinessRuleException => HttpStatusCode.Conflict,
                _ => HttpStatusCode.BadRequest,
            };

            context.Response.StatusCode = (int)status;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = (int)status,
                Title = status.ToString(),
                Detail = ex.Message,
            });
        }
    }
}
