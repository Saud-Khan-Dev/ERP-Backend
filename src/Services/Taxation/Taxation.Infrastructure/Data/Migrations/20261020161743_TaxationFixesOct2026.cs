using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Taxation.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class TaxationFixesOct2026 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "penalty_rule_code_key",
                schema: "taxation",
                table: "penalty_rule");

            migrationBuilder.DropIndex(
                name: "payment_transaction_ref_key",
                schema: "taxation",
                table: "payment");

            // the circle's officer post becomes its Taxation Officer staff row (the signer), where it has none yet
            migrationBuilder.Sql("""
                INSERT INTO taxation.tax_circle_staff (id, circle_id, post_id, tax_role, effective_from, created_at)
                SELECT gen_random_uuid(), c.id, c.officer_post_id, 'TO', DATE '2025-07-01', now()
                FROM taxation.tax_circle c
                WHERE c.officer_post_id IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM taxation.tax_circle_staff s WHERE s.circle_id = c.id AND s.tax_role = 'TO' AND s.effective_to IS NULL);
                """);

            migrationBuilder.DropColumn(
                name: "officer_post_id",
                schema: "taxation",
                table: "tax_circle");

            migrationBuilder.DropColumn(
                name: "other_label",
                schema: "taxation",
                table: "notice_table_layout");

            migrationBuilder.AddColumn<bool>(
                name: "charged_on_notice",
                schema: "taxation",
                table: "tax_head",
                type: "boolean",
                nullable: false,
                defaultValue: false);


            migrationBuilder.AddColumn<int>(
                name: "base_fiscal_year",
                schema: "taxation",
                table: "rate_set",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // the base year the sets were computed with until now: the fiscal year of their effective date
            migrationBuilder.Sql("""
                UPDATE taxation.rate_set
                SET base_fiscal_year = CASE WHEN EXTRACT(MONTH FROM effective_from) >= 7 THEN EXTRACT(YEAR FROM effective_from) ELSE EXTRACT(YEAR FROM effective_from) - 1 END;
                """);


            migrationBuilder.AlterColumn<string>(
                name: "transaction_ref",
                schema: "taxation",
                table: "payment",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(60)",
                oldMaxLength: 60);

            migrationBuilder.AlterColumn<Guid>(
                name: "tax_file_id",
                schema: "taxation",
                table: "payment",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "psid",
                schema: "taxation",
                table: "payment",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AddColumn<Guid>(
                name: "fee_id",
                schema: "taxation",
                table: "payment",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "party_id",
                schema: "taxation",
                table: "payment",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payer_name",
                schema: "taxation",
                table: "payment",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reversal_reason",
                schema: "taxation",
                table: "payment",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reversed_at",
                schema: "taxation",
                table: "payment",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reversed_by",
                schema: "taxation",
                table: "payment",
                type: "uuid",
                nullable: true);


            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                schema: "taxation",
                table: "party",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "cancelled_at",
                schema: "taxation",
                table: "notice",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cancelled_by",
                schema: "taxation",
                table: "notice",
                type: "uuid",
                nullable: true);





            migrationBuilder.AddColumn<Guid>(
                name: "consumed_by",
                schema: "taxation",
                table: "clearance",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payer_name",
                schema: "taxation",
                table: "clearance",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "revoked_at",
                schema: "taxation",
                table: "clearance",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "revoked_by",
                schema: "taxation",
                table: "clearance",
                type: "uuid",
                nullable: true);


            // no made-up references: a cash or counter payment carries its receipt number only
            migrationBuilder.Sql("UPDATE taxation.payment SET transaction_ref = NULL WHERE transaction_ref LIKE 'MAN-%' AND channel IN ('CASH', 'BANK_COUNTER');");
            // the catch-all "Miscellaneous" fee source is gone; such fees have no source recorded
            migrationBuilder.Sql("UPDATE taxation.fee SET source_type = NULL WHERE source_type = 'MISC';");
            // the charges the module carries on notices
            migrationBuilder.Sql("UPDATE taxation.tax_head SET charged_on_notice = true WHERE head_kind = 'OTHER' AND code IN ('LEASE_MONEY', 'SECURITY', 'SURCHARGE', 'WHT_236A', 'MISC');");

            migrationBuilder.CreateTable(
                name: "audit_log",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    entity = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    old_values = table.Column<string>(type: "jsonb", nullable: true),
                    new_values = table.Column<string>(type: "jsonb", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tax_file_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("audit_log_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "job_run",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    job = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    run_date = table.Column<DateOnly>(type: "date", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    started_by = table.Column<Guid>(type: "uuid", nullable: true),
                    year_end_surcharged = table.Column<int>(type: "integer", nullable: false),
                    notices_prepared = table.Column<int>(type: "integer", nullable: false),
                    summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("job_run_pkey", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "penalty_rule_code_effective_from_key",
                schema: "taxation",
                table: "penalty_rule",
                columns: new[] { "code", "effective_from" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payment_fee_id",
                schema: "taxation",
                table: "payment",
                column: "fee_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_party_id",
                schema: "taxation",
                table: "payment",
                column: "party_id");

            migrationBuilder.CreateIndex(
                name: "payment_receipt_no_key",
                schema: "taxation",
                table: "payment",
                column: "receipt_no",
                unique: true,
                filter: "receipt_no IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "payment_transaction_ref_key",
                schema: "taxation",
                table: "payment",
                column: "transaction_ref",
                unique: true,
                filter: "transaction_ref IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_at",
                schema: "taxation",
                table: "audit_log",
                column: "at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entity_entity_id",
                schema: "taxation",
                table: "audit_log",
                columns: new[] { "entity", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_tax_file_id",
                schema: "taxation",
                table: "audit_log",
                column: "tax_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_run_job_run_date",
                schema: "taxation",
                table: "job_run",
                columns: new[] { "job", "run_date" });

            migrationBuilder.AddForeignKey(
                name: "payment_fee_id_fkey",
                schema: "taxation",
                table: "payment",
                column: "fee_id",
                principalSchema: "taxation",
                principalTable: "fee",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "payment_party_id_fkey",
                schema: "taxation",
                table: "payment",
                column: "party_id",
                principalSchema: "taxation",
                principalTable: "party",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "payment_fee_id_fkey",
                schema: "taxation",
                table: "payment");

            migrationBuilder.DropForeignKey(
                name: "payment_party_id_fkey",
                schema: "taxation",
                table: "payment");

            migrationBuilder.DropTable(
                name: "audit_log",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "job_run",
                schema: "taxation");

            migrationBuilder.DropIndex(
                name: "penalty_rule_code_effective_from_key",
                schema: "taxation",
                table: "penalty_rule");

            migrationBuilder.DropIndex(
                name: "ix_payment_fee_id",
                schema: "taxation",
                table: "payment");

            migrationBuilder.DropIndex(
                name: "ix_payment_party_id",
                schema: "taxation",
                table: "payment");

            migrationBuilder.DropIndex(
                name: "payment_receipt_no_key",
                schema: "taxation",
                table: "payment");

            migrationBuilder.DropIndex(
                name: "payment_transaction_ref_key",
                schema: "taxation",
                table: "payment");

            migrationBuilder.DropColumn(
                name: "charged_on_notice",
                schema: "taxation",
                table: "tax_head");


            migrationBuilder.DropColumn(
                name: "base_fiscal_year",
                schema: "taxation",
                table: "rate_set");


            migrationBuilder.DropColumn(
                name: "fee_id",
                schema: "taxation",
                table: "payment");

            migrationBuilder.DropColumn(
                name: "party_id",
                schema: "taxation",
                table: "payment");

            migrationBuilder.DropColumn(
                name: "payer_name",
                schema: "taxation",
                table: "payment");

            migrationBuilder.DropColumn(
                name: "reversal_reason",
                schema: "taxation",
                table: "payment");

            migrationBuilder.DropColumn(
                name: "reversed_at",
                schema: "taxation",
                table: "payment");

            migrationBuilder.DropColumn(
                name: "reversed_by",
                schema: "taxation",
                table: "payment");


            migrationBuilder.DropColumn(
                name: "is_active",
                schema: "taxation",
                table: "party");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                schema: "taxation",
                table: "notice");

            migrationBuilder.DropColumn(
                name: "cancelled_by",
                schema: "taxation",
                table: "notice");





            migrationBuilder.DropColumn(
                name: "consumed_by",
                schema: "taxation",
                table: "clearance");

            migrationBuilder.DropColumn(
                name: "payer_name",
                schema: "taxation",
                table: "clearance");

            migrationBuilder.DropColumn(
                name: "revoked_at",
                schema: "taxation",
                table: "clearance");

            migrationBuilder.DropColumn(
                name: "revoked_by",
                schema: "taxation",
                table: "clearance");


            migrationBuilder.AddColumn<Guid>(
                name: "officer_post_id",
                schema: "taxation",
                table: "tax_circle",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "transaction_ref",
                schema: "taxation",
                table: "payment",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(60)",
                oldMaxLength: 60,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "tax_file_id",
                schema: "taxation",
                table: "payment",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "psid",
                schema: "taxation",
                table: "payment",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "other_label",
                schema: "taxation",
                table: "notice_table_layout",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Other charges / earlier dues");

            migrationBuilder.CreateIndex(
                name: "penalty_rule_code_key",
                schema: "taxation",
                table: "penalty_rule",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "payment_transaction_ref_key",
                schema: "taxation",
                table: "payment",
                column: "transaction_ref",
                unique: true);
        }
    }
}
