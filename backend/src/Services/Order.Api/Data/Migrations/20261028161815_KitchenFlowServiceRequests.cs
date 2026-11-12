using System;
using Microsoft.EntityFrameworkCore.Migrations;
using RestaurantAI.Infrastructure.Persistence.PostgreSQL;

#nullable disable

namespace RestaurantAI.Order.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class KitchenFlowServiceRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "history",
                schema: "orders",
                table: "orders",
                type: "jsonb",
                nullable: true,
                defaultValueSql: "'[]'::jsonb");

            // Existing orders start their history at the time they were placed.
            migrationBuilder.Sql("""
                UPDATE orders.orders
                SET history = jsonb_build_array(jsonb_build_object('Status', status, 'At', placed_at, 'By', customer_id))
                WHERE history IS NULL OR history = '[]'::jsonb;
                """);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                schema: "orders",
                table: "orders",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.CreateTable(
                name: "service_requests",
                schema: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    table_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    handled_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_requests", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_orders_tenant_id_branch_id_placed_at",
                schema: "orders",
                table: "orders",
                columns: new[] { "tenant_id", "branch_id", "placed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_service_requests_tenant_id_branch_id_status",
                schema: "orders",
                table: "service_requests",
                columns: new[] { "tenant_id", "branch_id", "status" });

            migrationBuilder.EnableTenantRowLevelSecurity("orders", "service_requests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRowLevelSecurity("orders", "service_requests");

            migrationBuilder.DropTable(
                name: "service_requests",
                schema: "orders");

            migrationBuilder.DropIndex(
                name: "ix_orders_tenant_id_branch_id_placed_at",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "history",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "orders",
                table: "orders");
        }
    }
}
