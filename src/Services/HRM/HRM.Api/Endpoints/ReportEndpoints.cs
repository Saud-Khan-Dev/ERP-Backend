/// Read-only reports over the HR, leave and pay records.
public class ReportEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    var reports = app.MapGroup("/reports").WithTags("Reports");

    reports.MapGet("/establishment", async (DateOnly? asOf, Guid? orgUnitId, ISender sender) =>
        (await sender.Send(new GetEstablishmentReportQuery(asOf, orgUnitId))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.View)
      .WithName("GetEstablishmentReport")
      .Produces<EstablishmentReport>()
      .WithSummary("Establishment (Sanctioned / Filled / Vacant)")
      .WithDescription("By unit, designation and BPS on a date (today by default). Abolished posts are left out; frozen seats are neither filled nor vacant. "
        + "orgUnitId takes in its sub-units.");

    reports.MapGet("/headcount", async (DateOnly? asOf, Guid? orgUnitId, ISender sender) =>
        (await sender.Send(new GetHeadcountReportQuery(asOf, orgUnitId))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.View)
      .WithName("GetHeadcountReport")
      .Produces<HeadcountReport>()
      .WithSummary("Headcount")
      .WithDescription("Employees holding a regular post on the date, by unit, BPS, employment type, status and gender.");

    reports.MapGet("/retirements", async (int? withinMonths, Guid? orgUnitId, ISender sender) =>
        (await sender.Send(new GetRetirementsReportQuery(withinMonths ?? 12, orgUnitId))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.View)
      .WithName("GetRetirementsReport")
      .Produces<RetirementsReport>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Retirements Due")
      .WithDescription("Employees in service reaching superannuation (Hrm:RetirementAge, 60 by default) within withinMonths (12 by default), soonest first; "
        + "anyone already past it is listed too. withoutDateOfBirth: employees the report cannot place.");

    reports.MapGet("/leave-balances", async (int? year, Guid? leaveTypeId, Guid? orgUnitId, IClock clock, ISender sender) =>
        (await sender.Send(new GetLeaveBalancesReportQuery(year ?? clock.Today.Year, leaveTypeId, orgUnitId))).ToOk())
      .RequirePermission(PermissionCatalog.Attendance.View)
      .WithName("GetLeaveBalancesReport")
      .Produces<LeaveBalancesReport>()
      .WithSummary("Leave Balances")
      .WithDescription("Every entitlement of the year with what was accrued, used and is left.");

    reports.MapGet("/payroll-summary", async (int? year, IClock clock, ISender sender) =>
        (await sender.Send(new GetPayrollSummaryReportQuery(year ?? clock.Today.Year))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.View)
      .WithName("GetPayrollSummaryReport")
      .Produces<PayrollSummaryReport>()
      .WithSummary("Payroll Summary")
      .WithDescription("Posted payroll (finalized and paid runs) of a calendar year, month by month, with totals per component.");

    reports.MapGet("/loans", async (Guid? loanTypeId, ISender sender) => (await sender.Send(new GetLoansReportQuery(loanTypeId))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.View)
      .WithName("GetLoansReport")
      .Produces<LoansReport>()
      .WithSummary("Loans Outstanding")
      .WithDescription("Active loans and advances: recovered, still owed, overdue and the next due date.");

    reports.MapGet("/tax", async (Guid? taxYearId, ISender sender) => (await sender.Send(new GetTaxReportQuery(taxYearId))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.View)
      .WithName("GetTaxReport")
      .Produces<TaxReport>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Income Tax Withheld")
      .WithDescription("Taxable pay and tax withheld so far in the tax year (the current one by default), per employee, with exemptions.");

    reports.MapGet("/gp-fund", async (bool? includeClosed, ISender sender) => (await sender.Send(new GetGpFundReportQuery(includeClosed ?? false))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.View)
      .WithName("GetGpFundReport")
      .Produces<GpFundReport>()
      .WithSummary("GP Fund Balances")
      .WithDescription("Every account with its subscription, total subscribed, interest credited and balance.");
  }
}
