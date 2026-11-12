using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantAI.Identity.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class LocalAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                schema: "identity",
                table: "user_accounts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<int>(
                name: "failed_logins",
                schema: "identity",
                table: "user_accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                schema: "identity",
                table: "user_accounts",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_login_at",
                schema: "identity",
                table: "user_accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "locked_until",
                schema: "identity",
                table: "user_accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "password_hash",
                schema: "identity",
                table: "user_accounts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "identity",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "failed_logins",
                schema: "identity",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "is_active",
                schema: "identity",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "last_login_at",
                schema: "identity",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "locked_until",
                schema: "identity",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "password_hash",
                schema: "identity",
                table: "user_accounts");
        }
    }
}
