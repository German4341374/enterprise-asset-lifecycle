using EnterpriseAssetLifecycle.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAssetLifecycle.Infrastructure;

public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, code, detail) = exception switch
        {
            ResourceNotFoundException => (
                StatusCodes.Status404NotFound,
                "Resource not found",
                "RESOURCE_NOT_FOUND",
                exception.Message),
            DomainRuleException domain => (
                StatusCodes.Status409Conflict,
                "Business rule rejected the operation",
                domain.Code,
                domain.Message),
            IdempotencyConflictException => (
                StatusCodes.Status409Conflict,
                "Idempotency conflict",
                "IDEMPOTENCY_CONFLICT",
                exception.Message),
            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "Concurrent update detected",
                "CONCURRENCY_CONFLICT",
                "The asset changed after it was read. Reload it and retry with the latest version."),
            DbUpdateException => (
                StatusCodes.Status409Conflict,
                "Database constraint rejected the operation",
                "DATABASE_CONFLICT",
                "A unique or relational constraint was violated."),
            BadHttpRequestException => (
                StatusCodes.Status400BadRequest,
                "Invalid request",
                "INVALID_REQUEST",
                exception.Message),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Unexpected error",
                "INTERNAL_ERROR",
                "The server could not complete the request.")
        };

        if (status >= 500)
        {
            logger.LogError(exception, "Unhandled request failure for {TraceId}", httpContext.TraceIdentifier);
        }
        else
        {
            logger.LogWarning("Request rejected with {Code} for {TraceId}: {Message}", code, httpContext.TraceIdentifier, detail);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path,
                Extensions =
                {
                    ["code"] = code,
                    ["traceId"] = httpContext.TraceIdentifier
                }
            }
        });
    }
}

