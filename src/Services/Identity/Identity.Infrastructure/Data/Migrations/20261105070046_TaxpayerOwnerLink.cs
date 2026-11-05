using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class TaxpayerOwnerLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "owner_id",
                schema: "auth",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_owner_id",
                schema: "auth",
                table: "users",
                column: "owner_id",
                unique: true,
                filter: "owner_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_owner_id",
                schema: "auth",
                table: "users");

            migrationBuilder.DropColumn(
                name: "owner_id",
                schema: "auth",
                table: "users");
        }
    }
}
