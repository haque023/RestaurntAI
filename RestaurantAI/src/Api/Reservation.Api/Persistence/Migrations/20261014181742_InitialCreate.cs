using System;
using Microsoft.EntityFrameworkCore.Migrations;
using RestaurantAI.Infrastructure.Persistence.PostgreSQL;

#nullable disable

namespace RestaurantAI.Reservation.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "reservations");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.CreateTable(
                name: "idempotency_entries",
                schema: "reservations",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    operation = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status_code = table.Column<int>(type: "integer", nullable: false),
                    response_body = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_idempotency_entries", x => new { x.tenant_id, x.key });
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "reservations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reservations",
                schema: "reservations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    table_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_size = table.Column<int>(type: "integer", nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    special_requests = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reservations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_entries_expires_at",
                schema: "reservations",
                table: "idempotency_entries",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_occurred_at",
                schema: "reservations",
                table: "outbox_messages",
                column: "occurred_at",
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_reservations_tenant_id_branch_id_starts_at",
                schema: "reservations",
                table: "reservations",
                columns: new[] { "tenant_id", "branch_id", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_reservations_tenant_id_customer_id",
                schema: "reservations",
                table: "reservations",
                columns: new[] { "tenant_id", "customer_id" });

            // Double booking is prevented transactionally: no two active reservations may
            // overlap on the same table.
            migrationBuilder.Sql("""
                ALTER TABLE "reservations"."reservations"
                    ADD CONSTRAINT "reservations_no_table_overlap"
                    EXCLUDE USING gist (
                        tenant_id WITH =,
                        table_id WITH =,
                        tstzrange(starts_at, ends_at, '[)') WITH &&)
                    WHERE (table_id IS NOT NULL AND status IN ('Pending', 'Confirmed', 'Seated'));
                """);

            // Tenant isolation layer 4: PostgreSQL Row-Level Security.
            migrationBuilder.EnableTenantRowLevelSecurity("reservations", "reservations");
            migrationBuilder.EnableTenantRowLevelSecurity("reservations", "idempotency_entries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_entries",
                schema: "reservations");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "reservations");

            migrationBuilder.DropTable(
                name: "reservations",
                schema: "reservations");
        }
    }
}
