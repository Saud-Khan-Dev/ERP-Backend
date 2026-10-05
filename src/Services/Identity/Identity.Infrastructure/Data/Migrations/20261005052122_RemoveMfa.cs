using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMfa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "mfa_enabled",
                schema: "auth",
                table: "users");

            migrationBuilder.DropColumn(
                name: "mfa_secret",
                schema: "auth",
                table: "users");

            migrationBuilder.DropColumn(
                name: "mfa_type",
                schema: "auth",
                table: "users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "mfa_enabled",
                schema: "auth",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "mfa_secret",
                schema: "auth",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "mfa_type",
                schema: "auth",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }
    }
}
