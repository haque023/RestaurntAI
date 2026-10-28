using System;
using Microsoft.EntityFrameworkCore.Migrations;
using RestaurantAI.Infrastructure.Persistence.PostgreSQL;

#nullable disable

namespace RestaurantAI.Restaurant.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LifecycleTablesQr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ai_enabled",
                schema: "restaurant",
                table: "restaurants",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ai_knowledge",
                schema: "restaurant",
                table: "restaurants",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ai_tone",
                schema: "restaurant",
                table: "restaurants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                schema: "restaurant",
                table: "restaurants",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<string>(
                name: "phone",
                schema: "restaurant",
                table: "restaurants",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "plan_code",
                schema: "restaurant",
                table: "restaurants",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "starter");

            migrationBuilder.AddColumn<string>(
                name: "suspension_reason",
                schema: "restaurant",
                table: "restaurants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "website",
                schema: "restaurant",
                table: "restaurants",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "dining_tables",
                schema: "restaurant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    qr_token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dining_tables", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "qr_codes",
                schema: "restaurant",
                columns: table => new
                {
                    token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    table_id = table.Column<Guid>(type: "uuid", nullable: false),
                    table_label = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_qr_codes", x => x.token);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dining_tables_qr_token",
                schema: "restaurant",
                table: "dining_tables",
                column: "qr_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dining_tables_tenant_id_branch_id_label",
                schema: "restaurant",
                table: "dining_tables",
                columns: new[] { "tenant_id", "branch_id", "label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_qr_codes_table_id",
                schema: "restaurant",
                table: "qr_codes",
                column: "table_id",
                unique: true);

            migrationBuilder.EnableTenantRowLevelSecurity("restaurant", "dining_tables");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRowLevelSecurity("restaurant", "dining_tables");

            migrationBuilder.DropTable(
                name: "dining_tables",
                schema: "restaurant");

            migrationBuilder.DropTable(
                name: "qr_codes",
                schema: "restaurant");

            migrationBuilder.DropColumn(
                name: "ai_enabled",
                schema: "restaurant",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "ai_knowledge",
                schema: "restaurant",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "ai_tone",
                schema: "restaurant",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "restaurant",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "phone",
                schema: "restaurant",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "plan_code",
                schema: "restaurant",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "suspension_reason",
                schema: "restaurant",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "website",
                schema: "restaurant",
                table: "restaurants");
        }
    }
}
