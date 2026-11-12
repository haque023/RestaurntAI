using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantAI.Restaurant.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Discovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "cuisines",
                schema: "restaurant",
                table: "restaurants",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");

            migrationBuilder.AddColumn<string>(
                name: "description",
                schema: "restaurant",
                table: "restaurants",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<List<string>>(
                name: "dietary_options",
                schema: "restaurant",
                table: "restaurants",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");

            migrationBuilder.AddColumn<double>(
                name: "latitude",
                schema: "restaurant",
                table: "branches",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                schema: "restaurant",
                table: "branches",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "opening_hours",
                schema: "restaurant",
                table: "branches",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "discovery_listings",
                schema: "restaurant",
                columns: table => new
                {
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    restaurant_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    cuisines = table.Column<List<string>>(type: "text[]", nullable: false),
                    dietary_options = table.Column<List<string>>(type: "text[]", nullable: false),
                    branch_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    opening_hours = table.Column<string>(type: "jsonb", nullable: false),
                    search_text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_discovery_listings", x => x.branch_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_discovery_listings_cuisines",
                schema: "restaurant",
                table: "discovery_listings",
                column: "cuisines")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_discovery_listings_dietary_options",
                schema: "restaurant",
                table: "discovery_listings",
                column: "dietary_options")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_discovery_listings_latitude_longitude",
                schema: "restaurant",
                table: "discovery_listings",
                columns: new[] { "latitude", "longitude" });

            migrationBuilder.CreateIndex(
                name: "ix_discovery_listings_tenant_id",
                schema: "restaurant",
                table: "discovery_listings",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "discovery_listings",
                schema: "restaurant");

            migrationBuilder.DropColumn(
                name: "cuisines",
                schema: "restaurant",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "description",
                schema: "restaurant",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "dietary_options",
                schema: "restaurant",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "latitude",
                schema: "restaurant",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "longitude",
                schema: "restaurant",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "opening_hours",
                schema: "restaurant",
                table: "branches");
        }
    }
}
