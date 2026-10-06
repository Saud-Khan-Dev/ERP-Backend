public sealed record CreateSalaryComponentRequest(string ComponentCode, string ComponentName, ComponentType ComponentType, bool IsTaxable);
public sealed record CloseRuleRequest(DateOnly LastDay);
public sealed record TryRuleRequest(SalaryRuleInput Rule, PayFiguresInput Figures);
public sealed record CreateSalaryOverrideRequest(Guid ComponentId, Guid? PostId, decimal? OverrideFixedAmount, decimal? OverridePercentage, DateOnly EffectiveFrom,
  DateOnly? EffectiveTo, string? Remarks);
public sealed record EndOverrideRequest(DateOnly LastDay);
public sealed record CreateTaxYearRequest(string YearLabel, DateOnly StartDate, DateOnly EndDate, IReadOnlyList<TaxSlabInput> Slabs);
public sealed record UpdateTaxYearRequest(string YearLabel, DateOnly StartDate, DateOnly EndDate);
public sealed record TaxSlabsRequest(IReadOnlyList<TaxSlabInput> Slabs);
public sealed record TaxExemptionRequest(Guid TaxYearId, string ExemptionType, decimal Amount);
public sealed record UpdateTaxExemptionRequest(string ExemptionType, decimal Amount);

/// The salary structure: pay-slip components, the versioned rules that work them out, employee overrides, and income
/// tax (tax years with slabs, exemptions, tax withheld so far).
public class PayStructureEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    // ---- components ----

    var components = app.MapGroup("/salary-components").WithTags("Salary Components");

    components.MapGet("/", async (bool? includeInactive, string? componentType, ISender sender) =>
        (await sender.Send(new GetSalaryComponentsQuery(includeInactive ?? false, QueryParsing.ParseEnum<ComponentType>(componentType, "componentType")))).ToOk())
      .RequireAnyPermission(PermissionCatalog.Payroll.View, PermissionCatalog.HrSetup.View)
      .WithName("GetSalaryComponents")
      .Produces<GetSalaryComponentsQueryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Salary Components")
      .WithDescription("Earnings then deductions. isSystem: BASIC, IT and GPF are worked out by the payroll engine. componentType = earning | deduction.");

    components.MapPost("/", async (CreateSalaryComponentRequest r, ISender sender) =>
        (await sender.Send(new CreateSalaryComponentCommand(r.ComponentCode, new SalaryComponentInput(r.ComponentName, r.ComponentType, r.IsTaxable))))
          .ToCreated(c => $"/salary-components/{c.Id}"))
      .RequirePermission(PermissionCatalog.Payroll.Create)
      .WithName("CreateSalaryComponent")
      .Produces<CreatedResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Create Salary Component")
      .WithDescription("A deduction is never taxable. The code is upper-cased and cannot change later.");

    components.MapPut("/{id:guid}", async (Guid id, SalaryComponentInput component, ISender sender) =>
        (await sender.Send(new UpdateSalaryComponentCommand(id, component))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Edit)
      .WithName("UpdateSalaryComponent")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update Salary Component")
      .WithDescription("The type cannot change once rules, loans or pay slips use the component.");

    foreach (var (route, active) in new[] { ("activate", true), ("deactivate", false) })
    {
      components.MapPost($"/{{id:guid}}/{route}", async (Guid id, ISender sender) => (await sender.Send(new SetSalaryComponentActivationCommand(id, active))).ToOk())
        .RequirePermission(PermissionCatalog.Payroll.Edit)
        .WithName(active ? "ActivateSalaryComponent" : "DeactivateSalaryComponent")
        .Produces<UpdatedResult>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary(active ? "Activate Salary Component" : "Deactivate Salary Component")
        .WithDescription(active ? "" : "Refused while rules are in force or active loan types recover on it. System components stay active.");
    }

    // ---- rules ----

    var rules = app.MapGroup("/salary-rules").WithTags("Salary Rules");

    rules.MapGet("/", async (Guid? componentId, DateOnly? inForceOn, bool? includeInactive, ISender sender) =>
        (await sender.Send(new GetSalaryRulesQuery(componentId, inForceOn, includeInactive ?? false))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.View)
      .WithName("GetSalaryRules")
      .Produces<GetSalaryRulesQueryResult>()
      .WithSummary("Get Salary Rules")
      .WithDescription("By component, then priority. inForceOn: only rules in force on that date. isUsed: on approved or finalized pay slips.");

    rules.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetSalaryRuleQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.View)
      .WithName("GetSalaryRule")
      .Produces<GetSalaryRuleQueryResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Salary Rule");

    app.MapPost("/salary-components/{id:guid}/rules", async (Guid id, SalaryRuleInput rule, ISender sender) =>
        (await sender.Send(new CreateSalaryRuleCommand(id, rule))).ToCreated(r => $"/salary-rules/{r.Id}"))
      .RequirePermission(PermissionCatalog.Payroll.Approve)
      .WithTags("Salary Rules")
      .WithName("CreateSalaryRule")
      .Produces<CreatedResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Create Salary Rule")
      .WithDescription("calculationMethod = fixed (fixedAmount) | percentage (percentage of calculationBase) | formula (formulaExpression, e.g. "
        + "\"max(1500, min_basic_pay * 0.45)\" or \"if(bps >= 17, 5000, 3000)\"; + - * /, comparisons, min, max, round, floor, ceil, abs, if) | "
        + "tiered (formulaExpression = JSON bands on calculationBase, e.g. [{\"upTo\":50000,\"amount\":1500},{\"upTo\":null,\"rate\":3}]: the first band "
        + "whose upTo is at or above the base gives a fixed amount or a rate % of the base). "
        + "calculationBase = basic_pay | min_basic_pay | max_basic_pay | gross_pay (deductions only). Formula variables: basic_pay, min_basic_pay, max_basic_pay, "
        + "gross_pay, bps, stage, days_payable, days_in_period. Scope: BPS range, designation, org unit, employment type (empty = everyone). "
        + "The lowest priority number wins, then the most specific scope, then the latest start.");

    rules.MapPut("/{id:guid}", async (Guid id, SalaryRuleInput rule, ISender sender) => (await sender.Send(new UpdateSalaryRuleCommand(id, rule))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Approve)
      .WithName("UpdateSalaryRule")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Correct Salary Rule")
      .WithDescription("Only a rule not yet on approved or finalized pay slips; otherwise revise it.");

    rules.MapPost("/{id:guid}/revise", async (Guid id, SalaryRuleInput rule, ISender sender) =>
        (await sender.Send(new ReviseSalaryRuleCommand(id, rule))).ToCreated(r => $"/salary-rules/{r.Id}"))
      .RequirePermission(PermissionCatalog.Payroll.Approve)
      .WithName("ReviseSalaryRule")
      .Produces<CreatedResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Revise Salary Rule")
      .WithDescription("A new version from rule.effectiveFrom (with its own ruleVersion); the current rule closes the day before.");

    rules.MapPost("/{id:guid}/close", async (Guid id, CloseRuleRequest request, ISender sender) =>
        (await sender.Send(new CloseSalaryRuleCommand(id, request.LastDay))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Approve)
      .WithName("CloseSalaryRule")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Close Salary Rule");

    foreach (var (route, active) in new[] { ("activate", true), ("deactivate", false) })
    {
      rules.MapPost($"/{{id:guid}}/{route}", async (Guid id, ISender sender) => (await sender.Send(new SetSalaryRuleStatusCommand(id, active))).ToOk())
        .RequirePermission(PermissionCatalog.Payroll.Approve)
        .WithName(active ? "ActivateSalaryRule" : "DeactivateSalaryRule")
        .Produces<UpdatedResult>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary(active ? "Activate Salary Rule" : "Deactivate Salary Rule");
    }

    app.MapPost("/salary-components/{id:guid}/rules/try", async (Guid id, TryRuleRequest request, ISender sender) =>
        (await sender.Send(new TrySalaryRuleQuery(id, request.Rule, request.Figures))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.View)
      .WithTags("Salary Rules")
      .WithName("TrySalaryRule")
      .Produces<TrySalaryRuleResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Try Salary Rule")
      .WithDescription("Works the rule out on the figures given without saving it: the monthly amount and the amount for daysPayable of daysInPeriod.");

    // ---- employee overrides ----

    app.MapGet("/employees/{id:guid}/salary-overrides", async (Guid id, bool? includeEnded, ISender sender) =>
        (await sender.Send(new GetSalaryOverridesQuery(id, includeEnded ?? false))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.View)
      .WithTags("Salary Overrides")
      .WithName("GetSalaryOverrides")
      .Produces<GetSalaryOverridesQueryResult>()
      .WithSummary("Get Employee Salary Overrides");

    app.MapPost("/employees/{id:guid}/salary-overrides", async (Guid id, CreateSalaryOverrideRequest r, ISender sender) =>
        (await sender.Send(new CreateSalaryOverrideCommand(id, r.ComponentId, r.PostId,
          new SalaryOverrideInput(r.OverrideFixedAmount, r.OverridePercentage, r.EffectiveFrom, r.EffectiveTo, r.Remarks)))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Create)
      .WithTags("Salary Overrides")
      .WithName("CreateSalaryOverride")
      .Produces<CreatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Override Component for Employee")
      .WithDescription("A fixed amount or a percentage (of the rule's base, or of basic pay without a rule) for one employee. postId: only while they hold that post. "
        + "Periods of one component may not overlap.");

    var overrides = app.MapGroup("/salary-overrides").WithTags("Salary Overrides");

    overrides.MapPut("/{id:guid}", async (Guid id, SalaryOverrideInput item, ISender sender) => (await sender.Send(new UpdateSalaryOverrideCommand(id, item))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Edit)
      .WithName("UpdateSalaryOverride")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Change Salary Override")
      .WithDescription("Only while it is not on approved or finalized pay slips; otherwise end it and add a new one.");

    overrides.MapPost("/{id:guid}/end", async (Guid id, EndOverrideRequest request, ISender sender) =>
        (await sender.Send(new EndSalaryOverrideCommand(id, request.LastDay))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Edit)
      .WithName("EndSalaryOverride")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("End Salary Override");

    overrides.MapDelete("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new DeleteSalaryOverrideCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Delete)
      .WithName("DeleteSalaryOverride")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Delete Salary Override")
      .WithDescription("Only one never paid on.");

    // ---- tax ----

    var tax = app.MapGroup("/tax-years").WithTags("Income Tax");

    tax.MapGet("/", async (bool? includeInactive, ISender sender) => (await sender.Send(new GetTaxYearsQuery(includeInactive ?? false))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.View)
      .WithName("GetTaxYears")
      .Produces<GetTaxYearsQueryResult>()
      .WithSummary("Get Tax Years")
      .WithDescription("Newest first, with their slabs.");

    tax.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetTaxYearQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.View)
      .WithName("GetTaxYear")
      .Produces<GetTaxYearQueryResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Tax Year");

    tax.MapPost("/", async (CreateTaxYearRequest r, ISender sender) =>
        (await sender.Send(new CreateTaxYearCommand(r.YearLabel, r.StartDate, r.EndDate, r.Slabs))).ToCreated(c => $"/tax-years/{c.Id}"))
      .RequirePermission(PermissionCatalog.Payroll.Approve)
      .WithName("CreateTaxYear")
      .Produces<CreatedResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status409Conflict)
      .WithSummary("Create Tax Year")
      .WithDescription("e.g. 2025-26 from 1 July to 30 June. Slabs are [minIncome, maxIncome) on annual taxable income: tax = fixedAmount + ratePercentage % of the "
        + "income above minIncome. Only the highest slab may be open-ended. Tax years may not overlap.");

    tax.MapPut("/{id:guid}", async (Guid id, UpdateTaxYearRequest r, ISender sender) =>
        (await sender.Send(new UpdateTaxYearCommand(id, r.YearLabel, r.StartDate, r.EndDate))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Approve)
      .WithName("UpdateTaxYear")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update Tax Year")
      .WithDescription("The dates cannot change once tax has been withheld in the year.");

    tax.MapPut("/{id:guid}/slabs", async (Guid id, TaxSlabsRequest request, ISender sender) =>
        (await sender.Send(new ReplaceTaxSlabsCommand(id, request.Slabs))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Approve)
      .WithName("ReplaceTaxSlabs")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Replace Tax Slabs")
      .WithDescription("A Finance Act amendment. Months already paid keep what was withheld; the rest of the year is worked out on the new slabs.");

    foreach (var (route, active) in new[] { ("activate", true), ("deactivate", false) })
    {
      tax.MapPost($"/{{id:guid}}/{route}", async (Guid id, ISender sender) => (await sender.Send(new SetTaxYearStatusCommand(id, active))).ToOk())
        .RequirePermission(PermissionCatalog.Payroll.Approve)
        .WithName(active ? "ActivateTaxYear" : "DeactivateTaxYear")
        .Produces<UpdatedResult>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary(active ? "Activate Tax Year" : "Deactivate Tax Year");
    }

    app.MapGet("/tax/calculate", async (decimal annualTaxableIncome, Guid? taxYearId, ISender sender) =>
        (await sender.Send(new CalculateTaxQuery(taxYearId, annualTaxableIncome))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.View)
      .WithTags("Income Tax")
      .WithName("CalculateTax")
      .Produces<TaxCalculationResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Calculate Income Tax")
      .WithDescription("Annual and monthly tax on an annual taxable income, for a tax year or the one in force today.");

    app.MapGet("/employees/{id:guid}/tax", async (Guid id, Guid? taxYearId, ISender sender) => (await sender.Send(new GetEmployeeTaxQuery(id, taxYearId))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.View)
      .WithTags("Income Tax")
      .WithName("GetEmployeeTax")
      .Produces<EmployeeTaxResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Employee Tax So Far")
      .WithDescription("Taxable income and tax withheld so far in the tax year (reversed runs left out), each payroll's share, and the exemptions.");

    app.MapGet("/employees/{id:guid}/tax-exemptions", async (Guid id, Guid? taxYearId, ISender sender) =>
        (await sender.Send(new GetTaxExemptionsQuery(id, taxYearId))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.View)
      .WithTags("Income Tax")
      .WithName("GetTaxExemptions")
      .Produces<GetTaxExemptionsQueryResult>()
      .WithSummary("Get Tax Exemptions");

    app.MapPost("/employees/{id:guid}/tax-exemptions", async (Guid id, TaxExemptionRequest r, ISender sender) =>
        (await sender.Send(new CreateTaxExemptionCommand(id, r.TaxYearId, r.ExemptionType, r.Amount))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Create)
      .WithTags("Income Tax")
      .WithName("CreateTaxExemption")
      .Produces<CreatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Record Tax Exemption")
      .WithDescription("An amount taken off the employee's annual taxable income (e.g. Zakat, donations); one entry per type and tax year.");

    var exemptions = app.MapGroup("/tax-exemptions").WithTags("Income Tax");

    exemptions.MapPut("/{id:guid}", async (Guid id, UpdateTaxExemptionRequest r, ISender sender) =>
        (await sender.Send(new UpdateTaxExemptionCommand(id, r.ExemptionType, r.Amount))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Edit)
      .WithName("UpdateTaxExemption")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update Tax Exemption");

    exemptions.MapDelete("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new DeleteTaxExemptionCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Delete)
      .WithName("DeleteTaxExemption")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Delete Tax Exemption");
  }
}
