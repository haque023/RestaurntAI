using Microsoft.EntityFrameworkCore.Migrations;

namespace RestaurantAI.Infrastructure.Persistence.PostgreSQL;

/// <summary>
/// Migration helpers that enable PostgreSQL Row-Level Security on tenant-owned tables.
/// Call from a migration after the table has been created.
/// </summary>
public static class RowLevelSecurityMigrationExtensions
{
    public static void EnableTenantRowLevelSecurity(this MigrationBuilder migrationBuilder, string schema, string table, string tenantColumn = "tenant_id")
    {
        var qualified = $"\"{schema}\".\"{table}\"";
        var policy = $"\"{table}_tenant_isolation\"";

        migrationBuilder.Sql($"ALTER TABLE {qualified} ENABLE ROW LEVEL SECURITY;");
        migrationBuilder.Sql($"ALTER TABLE {qualified} FORCE ROW LEVEL SECURITY;");
        migrationBuilder.Sql($"""
            CREATE POLICY {policy} ON {qualified}
                USING (
                    current_setting('{TenantSessionInterceptor.BypassSetting}', true) = 'on'
                    OR "{tenantColumn}" = NULLIF(current_setting('{TenantSessionInterceptor.TenantSetting}', true), '')::uuid)
                WITH CHECK (
                    current_setting('{TenantSessionInterceptor.BypassSetting}', true) = 'on'
                    OR "{tenantColumn}" = NULLIF(current_setting('{TenantSessionInterceptor.TenantSetting}', true), '')::uuid);
            """);
    }

    public static void DisableTenantRowLevelSecurity(this MigrationBuilder migrationBuilder, string schema, string table)
    {
        var qualified = $"\"{schema}\".\"{table}\"";
        migrationBuilder.Sql($"DROP POLICY IF EXISTS \"{table}_tenant_isolation\" ON {qualified};");
        migrationBuilder.Sql($"ALTER TABLE {qualified} NO FORCE ROW LEVEL SECURITY;");
        migrationBuilder.Sql($"ALTER TABLE {qualified} DISABLE ROW LEVEL SECURITY;");
    }
}
