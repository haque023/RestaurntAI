using System.Data.Common;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RestaurantAI.SharedKernel.Results;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Infrastructure.Idempotency;

public sealed record IdempotentRequest(Guid TenantId, string? Key, string Operation, string RequestHash, bool KeyRequired = true);

/// <summary>
/// Runs a mutation behind an <c>Idempotency-Key</c>: replays a stored response for a retried
/// request, rejects a reused key with a different payload, and commits the business change
/// together with the idempotency record.
/// </summary>
public static class IdempotentEndpoint
{
    public const string ReplayedHeader = "Idempotent-Replayed";

    public static async Task<IResult> ExecuteAsync<TContext, TResponse>(
        this IdempotencyStore<TContext> store,
        TContext dbContext,
        IdempotentRequest request,
        Func<CancellationToken, Task<Result<TResponse>>> execute,
        int successStatusCode,
        Func<TResponse, IResult> onSuccess,
        Func<Error, IResult> onFailure,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        Func<DbUpdateException, IResult?>? onDbConflict = null)
        where TContext : DbContext
    {
        if (request.Key is null && !request.KeyRequired)
        {
            var direct = await execute(cancellationToken);
            if (!direct.IsSuccess)
            {
                return onFailure(direct.Error);
            }

            return await SaveAsync(dbContext, () => onSuccess(direct.Value), onDbConflict, cancellationToken);
        }

        if (!IdempotencyStore<TContext>.IsValidKey(request.Key))
        {
            return TypedResults.Problem(
                title: "idempotency.key_required",
                detail: $"A valid '{PlatformHeaders.IdempotencyKey}' header is required (max 128 characters).",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var key = request.Key!;
        var lookup = await store.LookupAsync(request.TenantId, key, request.Operation, request.RequestHash, cancellationToken);
        if (lookup.Outcome != IdempotencyOutcome.New)
        {
            return ToReplayOrMismatch(lookup, httpContext);
        }

        var result = await execute(cancellationToken);
        if (!result.IsSuccess)
        {
            return onFailure(result.Error);
        }

        store.Record(request.TenantId, key, request.Operation, request.RequestHash, successStatusCode, result.Value);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return onSuccess(result.Value);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // A concurrent request with the same key committed first: replay its response.
            dbContext.ChangeTracker.Clear();
            var winner = await store.LookupAsync(request.TenantId, key, request.Operation, request.RequestHash, cancellationToken);
            return ToReplayOrMismatch(winner, httpContext);
        }
        catch (DbUpdateException ex) when (onDbConflict?.Invoke(ex) is { } conflict)
        {
            return conflict;
        }
    }

    public static bool IsUniqueViolation(this DbUpdateException exception) =>
        exception.InnerException is DbException { SqlState: "23505" };

    public static bool IsExclusionViolation(this DbUpdateException exception) =>
        exception.InnerException is DbException { SqlState: "23P01" };

    private static async Task<IResult> SaveAsync<TContext>(
        TContext dbContext,
        Func<IResult> onSuccess,
        Func<DbUpdateException, IResult?>? onDbConflict,
        CancellationToken cancellationToken)
        where TContext : DbContext
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return onSuccess();
        }
        catch (DbUpdateException ex) when (onDbConflict?.Invoke(ex) is { } conflict)
        {
            return conflict;
        }
    }

    private static IResult ToReplayOrMismatch(IdempotencyLookup lookup, HttpContext httpContext)
    {
        if (lookup.Outcome == IdempotencyOutcome.Replay)
        {
            httpContext.Response.Headers[ReplayedHeader] = "true";
            return Results.Content(lookup.ResponseBody, "application/json", Encoding.UTF8, lookup.StatusCode);
        }

        return TypedResults.Problem(
            title: "idempotency.key_reused",
            detail: "This Idempotency-Key was already used for a different request.",
            statusCode: StatusCodes.Status422UnprocessableEntity);
    }
}
