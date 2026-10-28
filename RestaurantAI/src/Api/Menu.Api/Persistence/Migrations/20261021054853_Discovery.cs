using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantAI.Menu.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Discovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "public_menu_items",
                schema: "menu",
                columns: table => new
                {
                    menu_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    dietary_tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    is_available = table.Column<bool>(type: "boolean", nullable: false),
                    search_text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_public_menu_items", x => x.menu_item_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_public_menu_items_branch_id",
                schema: "menu",
                table: "public_menu_items",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_public_menu_items_dietary_tags",
                schema: "menu",
                table: "public_menu_items",
                column: "dietary_tags")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "public_menu_items",
                schema: "menu");
        }
    }
}
