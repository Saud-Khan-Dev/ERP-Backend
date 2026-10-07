using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRM.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PerformanceItemOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                schema: "hrms",
                table: "performance_kpi",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                schema: "hrms",
                table: "performance_goal",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                schema: "hrms",
                table: "performance_competency",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "sort_order",
                schema: "hrms",
                table: "performance_kpi");

            migrationBuilder.DropColumn(
                name: "sort_order",
                schema: "hrms",
                table: "performance_goal");

            migrationBuilder.DropColumn(
                name: "sort_order",
                schema: "hrms",
                table: "performance_competency");
        }
    }
}
