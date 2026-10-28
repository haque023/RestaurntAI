using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RestaurantAI.SharedKernel.Domain;
using RestaurantAI.SharedKernel.Results;

namespace RestaurantAI.ServiceDefaults.Errors;

public static class PlatformProblemDetails
{
    public static IServiceCollection AddPlatformProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }

    /// <summary>Maps a failed <see cref="Result"/> to a standardized RFC 9457 problem response.</summary>
    public static IResult ToProblem(this Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError,
        };

        return TypedResults.Problem(
            title: error.Code,
            detail: error.Message,
            statusCode: status,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}

/// <summary>
/// Converts unhandled exceptions into problem details without leaking internal details.
/// </summary>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            // A business rule was violated by an otherwise well-formed request.
            DomainException domain => new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = domain.Code,
                Detail = domain.Message,
            },
            BadHttpRequestException bad => new ProblemDetails
            {
                Status = bad.StatusCode,
                Title = "bad_request",
                Detail = "The request is invalid.",
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "internal_error",
                Detail = "An unexpected error occurred.",
            },
        };

        if (problem.Status >= 500)
        {
            logger.LogError(exception, "Unhandled exception");
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
