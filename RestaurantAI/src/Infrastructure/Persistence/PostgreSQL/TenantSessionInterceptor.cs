using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Infrastructure.Persistence.PostgreSQL;

/// <summary>
/// Sets the PostgreSQL session variables consumed by Row-Level Security policies every time a
/// pooled connection is opened. Values are always overwritten so tenant context cannot leak
/// between requests that reuse the same physical connection.
/// </summary>
public sealed class TenantSessionInterceptor(ITenantContext tenantContext) : DbConnectionInterceptor
{
    public const string TenantSetting = "app.tenant_id";
    public const string BypassSetting = "app.bypass_rls";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = CreateCommand(connection);
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var command = CreateCommand(connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private DbCommand CreateCommand(DbConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"SELECT set_config('{TenantSetting}', @tenant, false), set_config('{BypassSetting}', @bypass, false);";

        var tenant = command.CreateParameter();
        tenant.ParameterName = "tenant";
        tenant.Value = tenantContext.TenantId?.ToString() ?? string.Empty;
        command.Parameters.Add(tenant);

        var bypass = command.CreateParameter();
        bypass.ParameterName = "bypass";
        bypass.Value = tenantContext.IsPlatformAdministrator ? "on" : "off";
        command.Parameters.Add(bypass);

        return command;
    }
}
