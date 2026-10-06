using FluentValidation;
using Microsoft.EntityFrameworkCore;

// ---- tax years and slabs ----

public sealed record GetTaxYearsQueryResult(IReadOnlyList<TaxYearDto> TaxYears);
public sealed record GetTaxYearsQuery(bool IncludeInactive) : IQuery<Result<GetTaxYearsQueryResult>>;

public sealed record GetTaxYearQueryResult(TaxYearDto TaxYear);
public sealed record GetTaxYearQuery(Guid Id) : IQuery<Result<GetTaxYearQueryResult>>;

public sealed record CreateTaxYearCommand(string YearLabel, DateOnly StartDate, DateOnly EndDate, IReadOnlyList<TaxSlabInput> Slabs) : ICommand<Result<CreatedResult>>;
public sealed record UpdateTaxYearCommand(Guid Id, string YearLabel, DateOnly StartDate, DateOnly EndDate) : ICommand<Result<UpdatedResult>>;

/// Replaces the slabs (a Finance Act amendment). Months already paid keep the tax withheld; the remaining months are
/// worked out on the new slabs.
public sealed record ReplaceTaxSlabsCommand(Guid Id, IReadOnlyList<TaxSlabInput> Slabs) : ICommand<Result<UpdatedResult>>;

public sealed record SetTaxYearStatusCommand(Guid Id, bool IsActive) : ICommand<Result<UpdatedResult>>;

public sealed record TaxCalculationResult(Guid TaxYearId, string YearLabel, decimal AnnualTaxableIncome, decimal AnnualTax, decimal MonthlyTax, decimal EffectiveRate);

/// The annual tax on an annual taxable income (for a tax year, or the one in force today).
public sealed record CalculateTaxQuery(Guid? TaxYearId, decimal AnnualTaxableIncome) : IQuery<Result<TaxCalculationResult>>;

public class TaxSlabInputValidator : AbstractValidator<TaxSlabInput>
{
  public TaxSlabInputValidator()
  {
    RuleFor(x => x.SlabOrder).GreaterThan(0);
    RuleFor(x => x.MinIncome).GreaterThanOrEqualTo(0);
    RuleFor(x => x.MaxIncome).GreaterThan(x => x.MinIncome).When(x => x.MaxIncome.HasValue).WithMessage("The slab maximum must be above its minimum.");
    RuleFor(x => x.FixedAmount).GreaterThanOrEqualTo(0);
    RuleFor(x => x.RatePercentage).InclusiveBetween(0, 100);
  }
}

public class CreateTaxYearCommandValidator : AbstractValidator<CreateTaxYearCommand>
{
  public CreateTaxYearCommandValidator()
  {
    RuleFor(x => x.YearLabel).NotEmpty().MaximumLength(20);
    RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate).WithMessage("A tax year must end after it starts.");
    RuleFor(x => x.Slabs).NotEmpty().WithMessage("Enter the tax slabs.");
    RuleForEach(x => x.Slabs).SetValidator(new TaxSlabInputValidator());
  }
}

public class UpdateTaxYearCommandValidator : AbstractValidator<UpdateTaxYearCommand>
{
  public UpdateTaxYearCommandValidator()
  {
    RuleFor(x => x.YearLabel).NotEmpty().MaximumLength(20);
    RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate).WithMessage("A tax year must end after it starts.");
  }
}

public class ReplaceTaxSlabsCommandValidator : AbstractValidator<ReplaceTaxSlabsCommand>
{
  public ReplaceTaxSlabsCommandValidator()
  {
    RuleFor(x => x.Slabs).NotEmpty().WithMessage("Enter the tax slabs.");
    RuleForEach(x => x.Slabs).SetValidator(new TaxSlabInputValidator());
  }
}

public class CalculateTaxQueryValidator : AbstractValidator<CalculateTaxQuery>
{
  public CalculateTaxQueryValidator() => RuleFor(x => x.AnnualTaxableIncome).GreaterThanOrEqualTo(0);
}

// ---- employee exemptions and tax so far ----

public sealed record GetTaxExemptionsQueryResult(IReadOnlyList<TaxExemptionDto> Exemptions);
public sealed record GetTaxExemptionsQuery(Guid EmployeeId, Guid? TaxYearId) : IQuery<Result<GetTaxExemptionsQueryResult>>;

public sealed record CreateTaxExemptionCommand(Guid EmployeeId, Guid TaxYearId, string ExemptionType, decimal Amount) : ICommand<Result<CreatedResult>>;
public sealed record UpdateTaxExemptionCommand(Guid Id, string ExemptionType, decimal Amount) : ICommand<Result<UpdatedResult>>;
public sealed record DeleteTaxExemptionCommand(Guid Id) : ICommand<Result<UpdatedResult>>;

public sealed record EmployeeTaxResult(
  Guid EmployeeId,
  Guid TaxYearId,
  string YearLabel,
  decimal TaxableIncomeYtd,
  decimal TaxWithheldYtd,
  decimal Exemptions,
  IReadOnlyList<TaxLedgerEntryDto> Entries);

/// Taxable income and tax withheld so far in a tax year (reversed runs left out), with each payroll's share.
public sealed record GetEmployeeTaxQuery(Guid EmployeeId, Guid? TaxYearId) : IQuery<Result<EmployeeTaxResult>>;

public class CreateTaxExemptionCommandValidator : AbstractValidator<CreateTaxExemptionCommand>
{
  public CreateTaxExemptionCommandValidator()
  {
    RuleFor(x => x.ExemptionType).NotEmpty().MaximumLength(100);
    RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
  }
}

public class UpdateTaxExemptionCommandValidator : AbstractValidator<UpdateTaxExemptionCommand>
{
  public UpdateTaxExemptionCommandValidator()
  {
    RuleFor(x => x.ExemptionType).NotEmpty().MaximumLength(100);
    RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
  }
}

public class TaxHandlers(IApplicationDbContext context, IClock clock) :
  IQueryHandler<GetTaxYearsQuery, Result<GetTaxYearsQueryResult>>,
  IQueryHandler<GetTaxYearQuery, Result<GetTaxYearQueryResult>>,
  ICommandHandler<CreateTaxYearCommand, Result<CreatedResult>>,
  ICommandHandler<UpdateTaxYearCommand, Result<UpdatedResult>>,
  ICommandHandler<ReplaceTaxSlabsCommand, Result<UpdatedResult>>,
  ICommandHandler<SetTaxYearStatusCommand, Result<UpdatedResult>>,
  IQueryHandler<CalculateTaxQuery, Result<TaxCalculationResult>>,
  IQueryHandler<GetTaxExemptionsQuery, Result<GetTaxExemptionsQueryResult>>,
  ICommandHandler<CreateTaxExemptionCommand, Result<CreatedResult>>,
  ICommandHandler<UpdateTaxExemptionCommand, Result<UpdatedResult>>,
  ICommandHandler<DeleteTaxExemptionCommand, Result<UpdatedResult>>,
  IQueryHandler<GetEmployeeTaxQuery, Result<EmployeeTaxResult>>
{
  // ---- tax years and slabs ----

  public async Task<Result<GetTaxYearsQueryResult>> Handle(GetTaxYearsQuery query, CancellationToken cancellationToken)
  {
    var years = await context.TaxYears.AsNoTracking().Include(t => t.Slabs)
      .Where(t => query.IncludeInactive || t.Status == RecordStatus.Active)
      .OrderByDescending(t => t.StartDate).ToListAsync(cancellationToken);
    return Result<GetTaxYearsQueryResult>.Success(new(years.Select(t => t.ToDto()).ToList()));
  }

  public async Task<Result<GetTaxYearQueryResult>> Handle(GetTaxYearQuery query, CancellationToken cancellationToken) =>
      Result<GetTaxYearQueryResult>.Success(new((await context.LoadTaxYearAsync(query.Id, cancellationToken)).ToDto()));

  public async Task<Result<CreatedResult>> Handle(CreateTaxYearCommand command, CancellationToken cancellationToken)
  {
    var year = TaxYear.Create(TaxYearId.New(), command.YearLabel, command.StartDate, command.EndDate, command.Slabs);
    if (await context.TaxYears.AnyAsync(t => t.YearLabel == year.YearLabel, cancellationToken))
      return Result<CreatedResult>.Failure($"Tax year {year.YearLabel} already exists.");

    context.TaxYears.Add(year);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(year.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(UpdateTaxYearCommand command, CancellationToken cancellationToken)
  {
    var year = await context.LoadTaxYearAsync(command.Id, cancellationToken);
    if ((year.StartDate != command.StartDate || year.EndDate != command.EndDate)
        && await context.TaxLedger.AnyAsync(l => l.TaxYearId == year.Id, cancellationToken))
      return Result<UpdatedResult>.Failure($"Tax has already been withheld in {year.YearLabel}; its dates cannot change.");

    year.Update(command.YearLabel, command.StartDate, command.EndDate);
    if (await context.TaxYears.AnyAsync(t => t.Id != year.Id && t.YearLabel == year.YearLabel, cancellationToken))
      return Result<UpdatedResult>.Failure($"Tax year {year.YearLabel} already exists.");

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(ReplaceTaxSlabsCommand command, CancellationToken cancellationToken)
  {
    var year = await context.LoadTaxYearAsync(command.Id, cancellationToken);
    year.ReplaceSlabs(command.Slabs);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(SetTaxYearStatusCommand command, CancellationToken cancellationToken)
  {
    var year = await context.LoadTaxYearAsync(command.Id, cancellationToken);
    year.SetStatus(command.IsActive ? RecordStatus.Active : RecordStatus.Inactive);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<TaxCalculationResult>> Handle(CalculateTaxQuery query, CancellationToken cancellationToken)
  {
    var year = await YearAsync(query.TaxYearId, cancellationToken);
    var tax = year.AnnualTax(query.AnnualTaxableIncome);
    var monthly = decimal.Round(tax / 12m, 2, MidpointRounding.AwayFromZero);
    var rate = query.AnnualTaxableIncome == 0 ? 0 : decimal.Round(tax * 100m / query.AnnualTaxableIncome, 2);
    return Result<TaxCalculationResult>.Success(new(year.Id.Value, year.YearLabel, query.AnnualTaxableIncome, tax, monthly, rate));
  }

  // ---- employee exemptions and tax so far ----

  public async Task<Result<GetTaxExemptionsQueryResult>> Handle(GetTaxExemptionsQuery query, CancellationToken cancellationToken)
  {
    var employeeId = EmployeeId.Of(query.EmployeeId);
    var rows = context.TaxExemptions.AsNoTracking().Where(e => e.EmployeeId == employeeId);
    if (query.TaxYearId is { } year)
    {
      var yearId = TaxYearId.Of(year);
      rows = rows.Where(e => e.TaxYearId == yearId);
    }

    var list = await rows.ToListAsync(cancellationToken);
    var labels = await context.TaxYears.AsNoTracking().ToDictionaryAsync(t => t.Id, t => new { t.YearLabel, t.StartDate }, cancellationToken);
    return Result<GetTaxExemptionsQueryResult>.Success(new(list
      .OrderByDescending(e => labels.GetValueOrDefault(e.TaxYearId)?.StartDate).ThenBy(e => e.ExemptionType)
      .Select(e => new TaxExemptionDto(e.Id.Value, e.EmployeeId.Value, e.TaxYearId.Value, labels.GetValueOrDefault(e.TaxYearId)?.YearLabel, e.ExemptionType, e.Amount))
      .ToList()));
  }

  public async Task<Result<CreatedResult>> Handle(CreateTaxExemptionCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.EmployeeId, cancellationToken);
    var year = await context.LoadTaxYearAsync(command.TaxYearId, cancellationToken);
    var exemption = EmployeeTaxExemption.Create(TaxExemptionId.New(), employee.Id, year, command.ExemptionType, command.Amount);
    if (await context.TaxExemptions.AnyAsync(e => e.EmployeeId == employee.Id && e.TaxYearId == year.Id
        && e.ExemptionType.ToLower() == exemption.ExemptionType.ToLower(), cancellationToken))
      return Result<CreatedResult>.Failure($"{exemption.ExemptionType} is already recorded for {year.YearLabel}; change that entry instead.");

    context.TaxExemptions.Add(exemption);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(exemption.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(UpdateTaxExemptionCommand command, CancellationToken cancellationToken)
  {
    var exemption = await LoadExemptionAsync(command.Id, cancellationToken);
    exemption.Update(command.ExemptionType, command.Amount);
    if (await context.TaxExemptions.AnyAsync(e => e.Id != exemption.Id && e.EmployeeId == exemption.EmployeeId && e.TaxYearId == exemption.TaxYearId
        && e.ExemptionType.ToLower() == exemption.ExemptionType.ToLower(), cancellationToken))
      return Result<UpdatedResult>.Failure($"{exemption.ExemptionType} is already recorded for that tax year.");

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(DeleteTaxExemptionCommand command, CancellationToken cancellationToken)
  {
    var exemption = await LoadExemptionAsync(command.Id, cancellationToken);
    context.TaxExemptions.Remove(exemption);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<EmployeeTaxResult>> Handle(GetEmployeeTaxQuery query, CancellationToken cancellationToken)
  {
    var employeeId = EmployeeId.Of(query.EmployeeId);
    if (!await context.Employees.AnyAsync(e => e.Id == employeeId, cancellationToken))
      throw new EmployeeNotFoundException($"Employee {query.EmployeeId} was not found.");

    var year = await YearAsync(query.TaxYearId, cancellationToken);
    var ytd = await context.EmployeeTaxYtd.AsNoTracking().FirstOrDefaultAsync(t => t.EmployeeId == employeeId.Value && t.TaxYearId == year.Id.Value, cancellationToken);
    var exemptions = await context.TaxExemptions.Where(e => e.EmployeeId == employeeId && e.TaxYearId == year.Id).SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0;

    var entries = await (
        from l in context.TaxLedger.AsNoTracking()
        where l.EmployeeId == employeeId && l.TaxYearId == year.Id
        join t in context.PayrollTransactions on l.PayrollTransactionId equals t.Id
        join r in context.PayrollRuns on t.PayrollRunId equals r.Id
        join p in context.PayrollPeriods on r.PayrollPeriodId equals p.Id
        where r.Status != PayrollRunStatus.Reversed
        orderby p.Year, p.Month, l.CreatedAt
        select new { l.Id, l.PayrollTransactionId, p.Year, p.Month, r.RunType, l.TaxableIncome, l.TaxWithheld, l.CreatedAt })
      .ToListAsync(cancellationToken);

    return Result<EmployeeTaxResult>.Success(new(employeeId.Value, year.Id.Value, year.YearLabel, ytd?.TaxableIncomeYtd ?? 0, ytd?.TaxWithheldYtd ?? 0, exemptions,
      entries.Select(e => new TaxLedgerEntryDto(e.Id.Value, e.PayrollTransactionId.Value, $"{e.Year}-{e.Month:00}", e.RunType, e.TaxableIncome, e.TaxWithheld, e.CreatedAt)).ToList()));
  }

  /// The given tax year, or the active one in force today.
  private async Task<TaxYear> YearAsync(Guid? id, CancellationToken cancellationToken)
  {
    if (id is { } given)
      return await context.LoadTaxYearAsync(given, cancellationToken);

    var today = clock.Today;
    return await context.TaxYears.AsNoTracking().Include(t => t.Slabs)
        .FirstOrDefaultAsync(t => t.Status == RecordStatus.Active && t.StartDate <= today && t.EndDate >= today, cancellationToken)
      ?? throw new TaxYearNotFoundException($"No active tax year covers {today:yyyy-MM-dd}. Add the tax year and its slabs.");
  }

  private async Task<EmployeeTaxExemption> LoadExemptionAsync(Guid id, CancellationToken cancellationToken)
  {
    var exemptionId = TaxExemptionId.Of(id);
    return await context.TaxExemptions.FirstOrDefaultAsync(e => e.Id == exemptionId, cancellationToken)
      ?? throw new TaxExemptionNotFoundException($"Tax exemption {id} was not found.");
  }
}
