using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRM.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class TaskFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "file_name",
                schema: "hrms",
                table: "employee_task_update",
                type: "varchar",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "file_path",
                schema: "hrms",
                table: "employee_task_update",
                type: "varchar",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_etu_file",
                schema: "hrms",
                table: "employee_task_update",
                sql: "(file_path IS NULL) = (file_name IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_etu_file",
                schema: "hrms",
                table: "employee_task_update");

            migrationBuilder.DropColumn(
                name: "file_name",
                schema: "hrms",
                table: "employee_task_update");

            migrationBuilder.DropColumn(
                name: "file_path",
                schema: "hrms",
                table: "employee_task_update");
        }
    }
}
