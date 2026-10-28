using System;
using Microsoft.EntityFrameworkCore.Migrations;
using RestaurantAI.Infrastructure.Persistence.PostgreSQL;

#nullable disable

namespace RestaurantAI.AI.Memory.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ai_memory");

            migrationBuilder.CreateTable(
                name: "conversation_turns",
                schema: "ai_memory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    token_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversation_turns", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "memory_facts",
                schema: "ai_memory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    content = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    confidence = table.Column<double>(type: "double precision", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_memory_facts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_conversation_turns_tenant_id_conversation_id_created_at",
                schema: "ai_memory",
                table: "conversation_turns",
                columns: new[] { "tenant_id", "conversation_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_memory_facts_tenant_id_user_id_category",
                schema: "ai_memory",
                table: "memory_facts",
                columns: new[] { "tenant_id", "user_id", "category" });

            // Tenant isolation layer 4: PostgreSQL Row-Level Security.
            migrationBuilder.EnableTenantRowLevelSecurity("ai_memory", "conversation_turns");
            migrationBuilder.EnableTenantRowLevelSecurity("ai_memory", "memory_facts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "conversation_turns",
                schema: "ai_memory");

            migrationBuilder.DropTable(
                name: "memory_facts",
                schema: "ai_memory");
        }
    }
}
