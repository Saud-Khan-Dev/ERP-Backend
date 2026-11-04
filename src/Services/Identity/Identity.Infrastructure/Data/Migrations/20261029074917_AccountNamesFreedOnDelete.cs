using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AccountNamesFreedOnDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_email",
                schema: "auth",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_employee_code",
                schema: "auth",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_username",
                schema: "auth",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                schema: "auth",
                table: "users",
                column: "email",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_users_employee_code",
                schema: "auth",
                table: "users",
                column: "employee_code",
                unique: true,
                filter: "employee_code IS NOT NULL AND employee_id IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_users_username",
                schema: "auth",
                table: "users",
                column: "username",
                unique: true,
                filter: "deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_email",
                schema: "auth",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_employee_code",
                schema: "auth",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_username",
                schema: "auth",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                schema: "auth",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_employee_code",
                schema: "auth",
                table: "users",
                column: "employee_code",
                unique: true,
                filter: "employee_code IS NOT NULL AND employee_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_users_username",
                schema: "auth",
                table: "users",
                column: "username",
                unique: true);
        }
    }
}
