using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRM.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class LeavePapers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "requires_document",
                schema: "hrms",
                table: "leave_type",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "supporting_document_id",
                schema: "hrms",
                table: "leave_application",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_la_supporting_document",
                schema: "hrms",
                table: "leave_application",
                column: "supporting_document_id");

            migrationBuilder.AddForeignKey(
                name: "leave_application_supporting_document_id_fkey",
                schema: "hrms",
                table: "leave_application",
                column: "supporting_document_id",
                principalSchema: "hrms",
                principalTable: "employee_document",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "leave_application_supporting_document_id_fkey",
                schema: "hrms",
                table: "leave_application");

            migrationBuilder.DropIndex(
                name: "idx_la_supporting_document",
                schema: "hrms",
                table: "leave_application");

            migrationBuilder.DropColumn(
                name: "requires_document",
                schema: "hrms",
                table: "leave_type");

            migrationBuilder.DropColumn(
                name: "supporting_document_id",
                schema: "hrms",
                table: "leave_application");
        }
    }
}
