// Read models over the schema's views. Keyless and read-only: the views are created by migration SQL and EF never
// changes them.

/// v_organization_unit_current: each unit's version in effect today, with its depth from the root (1 = top).
public class OrganizationUnitCurrent
{
  public Guid OrgUnitId { get; private set; }
  public string Code { get; private set; } = default!;
  public string Name { get; private set; } = default!;
  public Guid UnitTypeId { get; private set; }
  public Guid? ParentUnitId { get; private set; }
  public Guid? LocationId { get; private set; }
  public Guid? HeadPostId { get; private set; }
  public int? Level { get; private set; }
  public DateOnly EffectiveFrom { get; private set; }
}

/// v_post_occupancy: today's version of every post with its regular holders counted.
public class PostOccupancy
{
  public Guid PostId { get; private set; }
  public string PostCode { get; private set; } = default!;
  public Guid DesignationId { get; private set; }
  public Guid GradeId { get; private set; }
  public Guid OrgUnitId { get; private set; }
  public int SanctionedCount { get; private set; }
  public long FilledCount { get; private set; }
  public PositionStatus Status { get; private set; }
}

/// v_leave_balance: entitlement plus the signed ledger, per employee, leave type and year.
public class LeaveBalance
{
  public Guid EmployeeId { get; private set; }
  public Guid LeaveTypeId { get; private set; }
  public int Year { get; private set; }
  public decimal EntitledDays { get; private set; }
  public decimal AccruedDays { get; private set; }
  public decimal UsedDays { get; private set; }
  public decimal BalanceDays { get; private set; }
}

/// v_gp_fund_balance: each GP Fund account's balance and its subscription and interest totals.
public class GpFundBalance
{
  public Guid GpFundAccountId { get; private set; }
  public Guid EmployeeId { get; private set; }
  public decimal Balance { get; private set; }
  public decimal TotalSubscribed { get; private set; }
  public decimal TotalInterest { get; private set; }
}

/// v_employee_tax_ytd: taxable income and tax withheld so far in a tax year (reversed runs excluded).
public class EmployeeTaxYtd
{
  public Guid EmployeeId { get; private set; }
  public Guid TaxYearId { get; private set; }
  public decimal TaxableIncomeYtd { get; private set; }
  public decimal TaxWithheldYtd { get; private set; }
}
