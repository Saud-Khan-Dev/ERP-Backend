using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRM.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DutyRulesLoansAndLeave : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // a loan application waits for someone else to sanction it (duty rule LOAN_SANCTION); a label is never dropped
            migrationBuilder.Sql("ALTER TYPE hrms.loan_status_enum ADD VALUE IF NOT EXISTS 'pending';");

            // who keyed a leave application (duty rule LEAVE_APPROVE: leave entered on someone's behalf is decided by another)
            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "hrms",
                table: "leave_application",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "hrms",
                table: "leave_application");

        }
    }
}
