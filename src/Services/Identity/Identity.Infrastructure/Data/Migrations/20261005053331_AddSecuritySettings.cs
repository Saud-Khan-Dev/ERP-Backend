using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSecuritySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "security_settings",
                schema: "auth",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    password_minimum_length = table.Column<int>(type: "integer", nullable: false),
                    password_require_uppercase = table.Column<bool>(type: "boolean", nullable: false),
                    password_require_lowercase = table.Column<bool>(type: "boolean", nullable: false),
                    password_require_digit = table.Column<bool>(type: "boolean", nullable: false),
                    password_require_non_alphanumeric = table.Column<bool>(type: "boolean", nullable: false),
                    max_failed_login_attempts = table.Column<int>(type: "integer", nullable: false),
                    lockout_minutes = table.Column<int>(type: "integer", nullable: false),
                    access_token_minutes = table.Column<int>(type: "integer", nullable: false),
                    refresh_token_days = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_security_settings", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "security_settings",
                schema: "auth");
        }
    }
}
