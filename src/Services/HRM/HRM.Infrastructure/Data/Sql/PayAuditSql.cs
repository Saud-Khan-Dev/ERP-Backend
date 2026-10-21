using Microsoft.EntityFrameworkCore.Migrations;

/// Raw SQL of the PayAuditRepaymentsAndPer migration (2026-10-21): the review status "countersigned" (appended, in the
/// C# declaration order) and loan_repayment kept append-only like the other ledgers. Labels cannot be dropped.
public static class PayAuditSql
{
  public static void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.Sql("ALTER TYPE hrms.review_status_enum ADD VALUE IF NOT EXISTS 'countersigned';");
    migrationBuilder.Sql("CREATE TRIGGER trg_lr_append_only BEFORE UPDATE OR DELETE ON hrms.loan_repayment FOR EACH ROW EXECUTE FUNCTION hrms.fn_block_mutation();");
    migrationBuilder.Sql("COMMENT ON TABLE hrms.loan_repayment IS 'Append-only: money paid back on a loan outside payroll, with the receipt or challan and who recorded it.';");
    migrationBuilder.Sql("COMMENT ON TABLE hrms.employee_tax_opening IS 'Taxable income and tax withheld in a tax year before this payroll paid the employee; counted in the year-to-date.';");
  }

  public static void Down(MigrationBuilder migrationBuilder) =>
      migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_lr_append_only ON hrms.loan_repayment;");
}
