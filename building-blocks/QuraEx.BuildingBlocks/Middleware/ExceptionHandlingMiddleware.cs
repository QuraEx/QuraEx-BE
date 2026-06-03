using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ValidationException = QuraEx.BuildingBlocks.Exceptions.ValidationException;

namespace QuraEx.BuildingBlocks.Middleware;

/// <summary>Converts unhandled exceptions to RFC 7807 ProblemDetails responses.
/// Scrubs stack traces from non-development responses — full detail goes to OTel, never the HTTP body.</summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

            logger.LogError(
                ex,
                "Unhandled exception {TraceId} for {Method} {Path}",
                traceId,
                context.Request.Method,
                context.Request.Path);

            await WriteResponseAsync(context, ex, traceId);
        }
    }

    private async Task WriteResponseAsync(HttpContext context, Exception ex, string traceId)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        var (statusCode, problem) = MapException(ex, traceId);

        // Stack trace only in development — never expose internals on production responses
        if (env.IsDevelopment() && problem.Extensions is not null)
        {
            problem.Extensions["detail"] = ex.ToString();
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsJsonAsync(problem);
    }

    private static (int statusCode, ProblemDetails problem) MapException(Exception ex, string traceId)
    {
        return ex switch
        {
            Exceptions.NotFoundException nfe => (
                StatusCodes.Status404NotFound,
                new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Not Found",
                    Detail = nfe.Message,
                    Extensions = { ["traceId"] = traceId },
                }),

            ValidationException ve => (
                StatusCodes.Status422UnprocessableEntity,
                new ProblemDetails
                {
                    Status = StatusCodes.Status422UnprocessableEntity,
                    Title = "Validation Failed",
                    Detail = "One or more validation errors occurred.",
                    Extensions =
                    {
                        ["traceId"] = traceId,
                        ["errors"] = ve.Failures
                            .GroupBy(f => f.PropertyName)
                            .ToDictionary(
                                g => g.Key,
                                g => g.Select(f => f.ErrorMessage).ToArray()),
                    },
                }),

            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Conflict",
                    Detail = "The resource was modified by another request. Please retry.",
                    Extensions = { ["traceId"] = traceId },
                }),

            UnauthorizedAccessException => (
                StatusCodes.Status403Forbidden,
                new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Detail = "You do not have permission to perform this action.",
                    Extensions = { ["traceId"] = traceId },
                }),

            _ => (
                StatusCodes.Status500InternalServerError,
                new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Internal Server Error",
                    Detail = "An unexpected error occurred. Please try again later.",
                    Extensions = { ["traceId"] = traceId },
                }),
        };
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseQuraExExceptionHandling(this IApplicationBuilder app)
        => app.UseMiddleware<ExceptionHandlingMiddleware>();
}
