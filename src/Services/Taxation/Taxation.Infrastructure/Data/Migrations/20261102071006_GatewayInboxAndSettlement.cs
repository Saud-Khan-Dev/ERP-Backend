using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Taxation.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class GatewayInboxAndSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "integration_inbox",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    event_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    event_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    channel = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    psid = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    transaction_ref = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    bank_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tax_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("integration_inbox_pkey", x => x.id);
                    table.ForeignKey(
                        name: "integration_inbox_payment_id_fkey",
                        column: x => x.payment_id,
                        principalSchema: "taxation",
                        principalTable: "payment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "integration_inbox_tax_file_id_fkey",
                        column: x => x.tax_file_id,
                        principalSchema: "taxation",
                        principalTable: "tax_file",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "settlement_batch",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    channel = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    batch_ref = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    settlement_date = table.Column<DateOnly>(type: "date", nullable: false),
                    covers_from = table.Column<DateOnly>(type: "date", nullable: false),
                    covers_to = table.Column<DateOnly>(type: "date", nullable: false),
                    imported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    line_count = table.Column<int>(type: "integer", nullable: false),
                    matched_count = table.Column<int>(type: "integer", nullable: false),
                    exception_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("settlement_batch_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "settlement_line",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_ref = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    psid = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    match = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("settlement_line_pkey", x => x.id);
                    table.ForeignKey(
                        name: "settlement_line_batch_id_fkey",
                        column: x => x.batch_id,
                        principalSchema: "taxation",
                        principalTable: "settlement_batch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "settlement_line_payment_id_fkey",
                        column: x => x.payment_id,
                        principalSchema: "taxation",
                        principalTable: "payment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "integration_inbox_source_event_id_key",
                schema: "taxation",
                table: "integration_inbox",
                columns: new[] { "source", "event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_integration_inbox_payment_id",
                schema: "taxation",
                table: "integration_inbox",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "ix_integration_inbox_status_received_at",
                schema: "taxation",
                table: "integration_inbox",
                columns: new[] { "status", "received_at" });

            migrationBuilder.CreateIndex(
                name: "ix_integration_inbox_tax_file_id",
                schema: "taxation",
                table: "integration_inbox",
                column: "tax_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_integration_inbox_transaction_ref",
                schema: "taxation",
                table: "integration_inbox",
                column: "transaction_ref");

            migrationBuilder.CreateIndex(
                name: "settlement_batch_channel_batch_ref_key",
                schema: "taxation",
                table: "settlement_batch",
                columns: new[] { "channel", "batch_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_settlement_line_payment_id",
                schema: "taxation",
                table: "settlement_line",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "settlement_line_batch_id_transaction_ref_key",
                schema: "taxation",
                table: "settlement_line",
                columns: new[] { "batch_id", "transaction_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "settlement_line_matched_key",
                schema: "taxation",
                table: "settlement_line",
                column: "transaction_ref",
                unique: true,
                filter: "match = 'MATCHED'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "integration_inbox",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "settlement_line",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "settlement_batch",
                schema: "taxation");
        }
    }
}
