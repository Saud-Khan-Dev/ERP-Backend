using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "employee_code",
                schema: "auth",
                table: "users",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "employee_code_templates",
                schema: "auth",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    prefix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    separator = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    minimum_digits = table.Column<int>(type: "integer", nullable: false),
                    next_number = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employee_code_templates", x => x.id);
                    table.CheckConstraint("ck_employee_code_templates_minimum_digits", "minimum_digits BETWEEN 1 AND 10");
                    table.CheckConstraint("ck_employee_code_templates_next_number", "next_number >= 1");
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_employee_code",
                schema: "auth",
                table: "users",
                column: "employee_code",
                unique: true,
                filter: "employee_code IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employee_code_templates",
                schema: "auth");

            migrationBuilder.DropIndex(
                name: "ix_users_employee_code",
                schema: "auth",
                table: "users");

            migrationBuilder.DropColumn(
                name: "employee_code",
                schema: "auth",
                table: "users");
        }
    }
}
