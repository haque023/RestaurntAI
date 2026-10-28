using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using RestaurantAI.Restaurant.Api.Persistence;
using RestaurantAI.Restaurant.Domain;
using RestaurantAI.ServiceDefaults.Authorization;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Restaurant.Api.Endpoints;

/// <summary>Tables and their QR codes. Managers configure tables; floor staff update table status.</summary>
public static class TableEndpoints
{
    public sealed record TableRequest(string Label, int Capacity);

    public sealed record TableStatusRequest(TableStatus Status);

    public sealed record TableResponse(Guid Id, Guid BranchId, string Label, int Capacity, string Status, string QrToken)
    {
        public static TableResponse From(DiningTable t) => new(t.Id, t.BranchId, t.Label, t.Capacity, t.Status.ToString(), t.QrToken);
    }

    public static IEndpointRouteBuilder MapTableEndpoints(this IEndpointRouteBuilder app)
    {
        var tables = app.MapGroup("/api/restaurants/current").WithTags("Tables");

        tables.MapGet("/branches/{branchId:guid}/tables", ListAsync).RequirePermission(PlatformPermissions.OrdersRead);
        tables.MapPost("/branches/{branchId:guid}/tables", CreateAsync).RequirePermission(PlatformPermissions.RestaurantManage);
        tables.MapPut("/tables/{tableId:guid}", UpdateAsync).RequirePermission(PlatformPermissions.RestaurantManage);
        tables.MapDelete("/tables/{tableId:guid}", DeleteAsync).RequirePermission(PlatformPermissions.RestaurantManage);
        tables.MapPost("/tables/{tableId:guid}/rotate-qr", RotateQrAsync).RequirePermission(PlatformPermissions.RestaurantManage);
        tables.MapPut("/tables/{tableId:guid}/status", SetStatusAsync).RequirePermission(PlatformPermissions.OrdersManage);

        return app;
    }

    /// <summary>128 bits of randomness, URL-safe; unguessable so tables cannot be enumerated.</summary>
    public static string NewQrToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static async Task<IResult> ListAsync(Guid branchId, ITenantContext tenant, RestaurantDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!CanAccessBranch(tenant, branchId))
        {
            return BranchForbidden();
        }

        var tables = await dbContext.Tables.AsNoTracking()
            .Where(t => t.BranchId == branchId)
            .OrderBy(t => t.Label.Length).ThenBy(t => t.Label)
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(tables.Select(TableResponse.From).ToList());
    }

    private static async Task<IResult> CreateAsync(
        Guid branchId,
        TableRequest request,
        ITenantContext tenant,
        RestaurantDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!CanAccessBranch(tenant, branchId))
        {
            return BranchForbidden();
        }

        var restaurant = await dbContext.Restaurants.AsNoTracking().Include(r => r.Branches)
            .SingleOrDefaultAsync(r => r.Id == tenant.TenantId, cancellationToken);
        if (restaurant is null || restaurant.Branches.All(b => b.Id != branchId))
        {
            return TypedResults.NotFound();
        }

        var count = await dbContext.Tables.CountAsync(t => t.BranchId == branchId, cancellationToken);
        if (count >= restaurant.Plan.MaxTablesPerBranch)
        {
            return TypedResults.Problem(
                title: "table.limit",
                detail: $"The {restaurant.Plan.Name} plan allows {restaurant.Plan.MaxTablesPerBranch} tables per branch.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var label = request.Label?.Trim() ?? string.Empty;
        if (await dbContext.Tables.AnyAsync(t => t.BranchId == branchId && t.Label == label, cancellationToken))
        {
            return TypedResults.Problem(title: "table.duplicate_label", detail: $"Table '{label}' already exists in this branch.", statusCode: StatusCodes.Status409Conflict);
        }

        var table = DiningTable.Create(restaurant.Id, branchId, label, request.Capacity, NewQrToken(), timeProvider.GetUtcNow());
        dbContext.Tables.Add(table);
        dbContext.QrCodes.Add(new QrCodeRow { Token = table.QrToken, TenantId = table.TenantId, BranchId = branchId, TableId = table.Id, TableLabel = table.Label });
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/restaurants/current/tables/{table.Id}", TableResponse.From(table));
    }

    private static Task<IResult> UpdateAsync(Guid tableId, TableRequest request, ITenantContext tenant, RestaurantDbContext dbContext, CancellationToken cancellationToken) =>
        MutateAsync(tableId, tenant, dbContext, cancellationToken, async (table, qr) =>
        {
            var label = request.Label?.Trim() ?? string.Empty;
            if (await dbContext.Tables.AnyAsync(t => t.BranchId == table.BranchId && t.Label == label && t.Id != table.Id, cancellationToken))
            {
                return TypedResults.Problem(title: "table.duplicate_label", detail: $"Table '{label}' already exists in this branch.", statusCode: StatusCodes.Status409Conflict);
            }

            table.Update(label, request.Capacity);
            if (qr is not null)
            {
                qr.TableLabel = table.Label;
            }

            return TypedResults.Ok(TableResponse.From(table));
        });

    private static Task<IResult> RotateQrAsync(Guid tableId, ITenantContext tenant, RestaurantDbContext dbContext, CancellationToken cancellationToken) =>
        MutateAsync(tableId, tenant, dbContext, cancellationToken, (table, qr) =>
        {
            if (qr is not null)
            {
                dbContext.QrCodes.Remove(qr);
            }

            table.RotateQr(NewQrToken());
            dbContext.QrCodes.Add(new QrCodeRow { Token = table.QrToken, TenantId = table.TenantId, BranchId = table.BranchId, TableId = table.Id, TableLabel = table.Label });
            return Task.FromResult<IResult>(TypedResults.Ok(TableResponse.From(table)));
        });

    private static Task<IResult> SetStatusAsync(Guid tableId, TableStatusRequest request, ITenantContext tenant, RestaurantDbContext dbContext, CancellationToken cancellationToken) =>
        MutateAsync(tableId, tenant, dbContext, cancellationToken, (table, _) =>
        {
            table.SetStatus(request.Status);
            return Task.FromResult<IResult>(TypedResults.Ok(TableResponse.From(table)));
        });

    private static Task<IResult> DeleteAsync(Guid tableId, ITenantContext tenant, RestaurantDbContext dbContext, CancellationToken cancellationToken) =>
        MutateAsync(tableId, tenant, dbContext, cancellationToken, (table, qr) =>
        {
            if (qr is not null)
            {
                dbContext.QrCodes.Remove(qr);
            }

            dbContext.Tables.Remove(table);
            return Task.FromResult<IResult>(TypedResults.NoContent());
        });

    private static async Task<IResult> MutateAsync(
        Guid tableId,
        ITenantContext tenant,
        RestaurantDbContext dbContext,
        CancellationToken cancellationToken,
        Func<DiningTable, QrCodeRow?, Task<IResult>> change)
    {
        var table = await dbContext.Tables.SingleOrDefaultAsync(t => t.Id == tableId, cancellationToken);
        if (table is null)
        {
            return TypedResults.NotFound();
        }

        if (!CanAccessBranch(tenant, table.BranchId))
        {
            return BranchForbidden();
        }

        var qr = await dbContext.QrCodes.SingleOrDefaultAsync(q => q.TableId == tableId, cancellationToken);
        var result = await change(table, qr);
        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    private static bool CanAccessBranch(ITenantContext tenant, Guid branchId) =>
        tenant.IsPlatformAdministrator || tenant.BranchId is null || tenant.BranchId == branchId;

    private static IResult BranchForbidden() =>
        TypedResults.Problem(title: "branch.forbidden", detail: "You do not have access to this branch.", statusCode: StatusCodes.Status403Forbidden);
}
