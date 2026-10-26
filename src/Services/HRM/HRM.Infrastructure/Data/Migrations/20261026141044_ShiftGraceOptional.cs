using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRM.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ShiftGraceOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "work_shift_grace_minutes_check",
                schema: "hrms",
                table: "work_shift");

            migrationBuilder.AlterColumn<int>(
                name: "grace_minutes",
                schema: "hrms",
                table: "work_shift",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "work_shift_grace_minutes_check",
                schema: "hrms",
                table: "work_shift",
                sql: "grace_minutes IS NULL OR grace_minutes >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "work_shift_grace_minutes_check",
                schema: "hrms",
                table: "work_shift");

            migrationBuilder.AlterColumn<int>(
                name: "grace_minutes",
                schema: "hrms",
                table: "work_shift",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "work_shift_grace_minutes_check",
                schema: "hrms",
                table: "work_shift",
                sql: "grace_minutes >= 0");
        }
    }
}
