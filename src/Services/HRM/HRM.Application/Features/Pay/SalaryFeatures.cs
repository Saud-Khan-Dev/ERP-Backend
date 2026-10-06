using FluentValidation;
using Microsoft.EntityFrameworkCore;

// ---- components ----

public sealed record SalaryComponentInput(string ComponentName, ComponentType ComponentType, bool IsTaxable);

public sealed record GetSalaryComponentsQueryResult(IReadOnlyList<SalaryComponentDto> Components);
public sealed record GetSalaryComponentsQuery(bool IncludeInactive, ComponentType? ComponentType) : IQuery<Result<GetSalaryComponentsQueryResult>>;
public sealed record CreateSalaryComponentCommand(string ComponentCode, SalaryComponentInput Component) : ICommand<Result<CreatedResult>>;
public sealed record UpdateSalaryComponentCommand(Guid Id, SalaryComponentInput Component) : ICommand<Result<UpdatedResult>>;
public sealed record SetSalaryComponentActivationCommand(Guid Id, bool IsActive) : ICommand<Result<UpdatedResult>>;

public class CreateSalaryComponentCommandValidator : AbstractValidator<CreateSalaryComponentCommand>
{
  public CreateSalaryComponentCommandValidator()
  {
    RuleFor(x => x.ComponentCode).NotEmpty().MaximumLength(30);
    RuleFor(x => x.Component).NotNull();
    RuleFor(x => x.Component.ComponentName).NotEmpty().MaximumLength(150);
  }
}

public class UpdateSalaryComponentCommandValidator : AbstractValidator<UpdateSalaryComponentCommand>
{
  public UpdateSalaryComponentCommandValidator()
  {
    RuleFor(x => x.Component).NotNull();
    RuleFor(x => x.Component.ComponentName).NotEmpty().MaximumLength(150);
  }
}

// ---- rules ----

public sealed record SalaryRuleInput(
  string RuleVersion,
  CalculationMethod CalculationMethod,
  decimal? FixedAmount,
  decimal? Percentage,
  string? CalculationBase,
  string? FormulaExpression,
  int? MinBps,
  int? MaxBps,
  Guid? ApplicableDesignationId,
  Guid? ApplicableOrgUnitId,
  EmploymentType? ApplicableEmploymentType,
  decimal? MinAmount,
  decimal? MaxAmount,
  int Priority,
  string? NotificationRef,
  DateOnly EffectiveFrom,
  DateOnly? EffectiveTo)
{
  public SalaryRuleDefinition ToDefinition() => new(RuleVersion, CalculationMethod, FixedAmount, Percentage, CalculationBase, FormulaExpression,
    MinBps, MaxBps, ApplicableDesignationId is { } designation ? DesignationId.Of(designation) : null,
    ApplicableOrgUnitId is { } unit ? OrganizationUnitId.Of(unit) : null, ApplicableEmploymentType, MinAmount, MaxAmount, Priority, NotificationRef,
    EffectiveFrom, EffectiveTo);
}

/// The monthly figures to try a rule on. Missing ones default to the basic pay (bases) and a full 30-day month.
public sealed record PayFiguresInput(decimal BasicPay, decimal? MinBasicPay, decimal? MaxBasicPay, decimal? GrossPay, int Bps, int Stage, decimal? DaysPayable, int? DaysInPeriod)
{
  public PayFigures ToFigures() => new(BasicPay, MinBasicPay ?? BasicPay, MaxBasicPay ?? BasicPay, GrossPay ?? BasicPay, Bps, Stage, DaysPayable ?? DaysInPeriod ?? 30, DaysInPeriod ?? 30);
}

public sealed record GetSalaryRulesQueryResult(IReadOnlyList<SalaryRuleDto> Rules);

/// InForceOn: only rules in force on that date. Without a component, every component's rules.
public sealed record GetSalaryRulesQuery(Guid? ComponentId, DateOnly? InForceOn, bool IncludeInactive) : IQuery<Result<GetSalaryRulesQueryResult>>;

public sealed record GetSalaryRuleQueryResult(SalaryRuleDto Rule);
public sealed record GetSalaryRuleQuery(Guid Id) : IQuery<Result<GetSalaryRuleQueryResult>>;

public sealed record CreateSalaryRuleCommand(Guid ComponentId, SalaryRuleInput Rule) : ICommand<Result<CreatedResult>>;

/// Corrects a rule not yet on any approved or finalized pay slip.
public sealed record UpdateSalaryRuleCommand(Guid Id, SalaryRuleInput Rule) : ICommand<Result<UpdatedResult>>;

/// A new version from Rule.EffectiveFrom: the current rule closes the day before (the way a notified revision is made).
public sealed record ReviseSalaryRuleCommand(Guid Id, SalaryRuleInput Rule) : ICommand<Result<CreatedResult>>;

public sealed record CloseSalaryRuleCommand(Guid Id, DateOnly LastDay) : ICommand<Result<UpdatedResult>>;
public sealed record SetSalaryRuleStatusCommand(Guid Id, bool IsActive) : ICommand<Result<UpdatedResult>>;

public sealed record TrySalaryRuleResult(decimal Amount, string? CalculationBase, decimal? BaseAmount, decimal? Rate, string? FormulaReference, decimal ProratedAmount);

/// Works a rule out on given figures without saving it (to check a formula or tiers before notifying it).
public sealed record TrySalaryRuleQuery(Guid ComponentId, SalaryRuleInput Rule, PayFiguresInput Figures) : IQuery<Result<TrySalaryRuleResult>>;

public class SalaryRuleInputValidator : AbstractValidator<SalaryRuleInput>
{
  public SalaryRuleInputValidator()
  {
    RuleFor(x => x.RuleVersion).NotEmpty().MaximumLength(50);
    RuleFor(x => x.CalculationMethod).IsInEnum();
    RuleFor(x => x.FormulaExpression).MaximumLength(4000);
    RuleFor(x => x.NotificationRef).MaximumLength(200);
    RuleFor(x => x.MinBps).InclusiveBetween(1, 22).When(x => x.MinBps.HasValue);
    RuleFor(x => x.MaxBps).InclusiveBetween(1, 22).When(x => x.MaxBps.HasValue);
    RuleFor(x => x.Priority).InclusiveBetween(0, 1000);
    RuleFor(x => x.EffectiveTo).GreaterThanOrEqualTo(x => x.EffectiveFrom).When(x => x.EffectiveTo.HasValue)
      .WithMessage("The rule cannot end before it starts.");
  }
}

public class CreateSalaryRuleCommandValidator : AbstractValidator<CreateSalaryRuleCommand>
{
  public CreateSalaryRuleCommandValidator() => RuleFor(x => x.Rule).NotNull().SetValidator(new SalaryRuleInputValidator());
}

public class UpdateSalaryRuleCommandValidator : AbstractValidator<UpdateSalaryRuleCommand>
{
  public UpdateSalaryRuleCommandValidator() => RuleFor(x => x.Rule).NotNull().SetValidator(new SalaryRuleInputValidator());
}

public class ReviseSalaryRuleCommandValidator : AbstractValidator<ReviseSalaryRuleCommand>
{
  public ReviseSalaryRuleCommandValidator() => RuleFor(x => x.Rule).NotNull().SetValidator(new SalaryRuleInputValidator());
}

public class TrySalaryRuleQueryValidator : AbstractValidator<TrySalaryRuleQuery>
{
  public TrySalaryRuleQueryValidator()
  {
    RuleFor(x => x.Rule).NotNull().SetValidator(new SalaryRuleInputValidator());
    RuleFor(x => x.Figures).NotNull();
    RuleFor(x => x.Figures.BasicPay).GreaterThanOrEqualTo(0);
    RuleFor(x => x.Figures.Bps).InclusiveBetween(1, 22);
    RuleFor(x => x.Figures.Stage).GreaterThanOrEqualTo(0);
    RuleFor(x => x.Figures.DaysInPeriod).InclusiveBetween(28, 31).When(x => x.Figures?.DaysInPeriod is not null);
  }
}

// ---- employee overrides ----

public sealed record SalaryOverrideInput(decimal? OverrideFixedAmount, decimal? OverridePercentage, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string? Remarks);

public sealed record GetSalaryOverridesQueryResult(IReadOnlyList<SalaryOverrideDto> Overrides);
public sealed record GetSalaryOverridesQuery(Guid EmployeeId, bool IncludeEnded) : IQuery<Result<GetSalaryOverridesQueryResult>>;

/// An employee-specific amount for a component: fixed, or a percentage of the rule's base. PostId: only while the
/// employee holds that post.
public sealed record CreateSalaryOverrideCommand(Guid EmployeeId, Guid ComponentId, Guid? PostId, SalaryOverrideInput Override) : ICommand<Result<CreatedResult>>;

public sealed record UpdateSalaryOverrideCommand(Guid Id, SalaryOverrideInput Override) : ICommand<Result<UpdatedResult>>;
public sealed record EndSalaryOverrideCommand(Guid Id, DateOnly LastDay) : ICommand<Result<UpdatedResult>>;
public sealed record DeleteSalaryOverrideCommand(Guid Id) : ICommand<Result<UpdatedResult>>;

public class SalaryOverrideInputValidator : AbstractValidator<SalaryOverrideInput>
{
  public SalaryOverrideInputValidator()
  {
    RuleFor(x => x).Must(x => x.OverrideFixedAmount.HasValue != x.OverridePercentage.HasValue)
      .WithMessage("Give either a fixed amount or a percentage.");
    RuleFor(x => x.OverrideFixedAmount).GreaterThanOrEqualTo(0).When(x => x.OverrideFixedAmount.HasValue);
    RuleFor(x => x.OverridePercentage).InclusiveBetween(0, 1000).When(x => x.OverridePercentage.HasValue);
    RuleFor(x => x.EffectiveTo).GreaterThanOrEqualTo(x => x.EffectiveFrom).When(x => x.EffectiveTo.HasValue)
      .WithMessage("The override cannot end before it starts.");
    RuleFor(x => x.Remarks).MaximumLength(2000);
  }
}

public class CreateSalaryOverrideCommandValidator : AbstractValidator<CreateSalaryOverrideCommand>
{
  public CreateSalaryOverrideCommandValidator() => RuleFor(x => x.Override).NotNull().SetValidator(new SalaryOverrideInputValidator());
}

public class UpdateSalaryOverrideCommandValidator : AbstractValidator<UpdateSalaryOverrideCommand>
{
  public UpdateSalaryOverrideCommandValidator() => RuleFor(x => x.Override).NotNull().SetValidator(new SalaryOverrideInputValidator());
}

public class SalaryHandlers(IApplicationDbContext context, HrLookup lookup, ICurrentUser currentUser, IClock clock) :
  IQueryHandler<GetSalaryComponentsQuery, Result<GetSalaryComponentsQueryResult>>,
  ICommandHandler<CreateSalaryComponentCommand, Result<CreatedResult>>,
  ICommandHandler<UpdateSalaryComponentCommand, Result<UpdatedResult>>,
  ICommandHandler<SetSalaryComponentActivationCommand, Result<UpdatedResult>>,
  IQueryHandler<GetSalaryRulesQuery, Result<GetSalaryRulesQueryResult>>,
  IQueryHandler<GetSalaryRuleQuery, Result<GetSalaryRuleQueryResult>>,
  ICommandHandler<CreateSalaryRuleCommand, Result<CreatedResult>>,
  ICommandHandler<UpdateSalaryRuleCommand, Result<UpdatedResult>>,
  ICommandHandler<ReviseSalaryRuleCommand, Result<CreatedResult>>,
  ICommandHandler<CloseSalaryRuleCommand, Result<UpdatedResult>>,
  ICommandHandler<SetSalaryRuleStatusCommand, Result<UpdatedResult>>,
  IQueryHandler<TrySalaryRuleQuery, Result<TrySalaryRuleResult>>,
  IQueryHandler<GetSalaryOverridesQuery, Result<GetSalaryOverridesQueryResult>>,
  ICommandHandler<CreateSalaryOverrideCommand, Result<CreatedResult>>,
  ICommandHandler<UpdateSalaryOverrideCommand, Result<UpdatedResult>>,
  ICommandHandler<EndSalaryOverrideCommand, Result<UpdatedResult>>,
  ICommandHandler<DeleteSalaryOverrideCommand, Result<UpdatedResult>>
{
  /// Runs whose pay slips are settled enough that the rules behind them must stay as they were.
  private static readonly PayrollRunStatus[] Settled = [PayrollRunStatus.Approved, PayrollRunStatus.Finalized, PayrollRunStatus.Paid];

  // ---- components ----

  public async Task<Result<GetSalaryComponentsQueryResult>> Handle(GetSalaryComponentsQuery query, CancellationToken cancellationToken)
  {
    var rows = context.SalaryComponents.AsNoTracking().Where(c => query.IncludeInactive || c.IsActive);
    if (query.ComponentType is { } type)
      rows = rows.Where(c => c.ComponentType == type);

    var list = await rows.OrderBy(c => c.ComponentType).ThenBy(c => c.ComponentName).ToListAsync(cancellationToken);
    return Result<GetSalaryComponentsQueryResult>.Success(new(list.Select(c => c.ToDto()).ToList()));
  }

  public async Task<Result<CreatedResult>> Handle(CreateSalaryComponentCommand command, CancellationToken cancellationToken)
  {
    var i = command.Component;
    var component = SalaryComponent.Create(SalaryComponentId.New(), command.ComponentCode, i.ComponentName, i.ComponentType, i.IsTaxable);
    if (await context.SalaryComponents.AnyAsync(c => c.ComponentCode == component.ComponentCode || c.ComponentName.ToLower() == component.ComponentName.ToLower(), cancellationToken))
      return Result<CreatedResult>.Failure($"A component coded {component.ComponentCode} or named '{component.ComponentName}' already exists.");

    context.SalaryComponents.Add(component);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(component.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(UpdateSalaryComponentCommand command, CancellationToken cancellationToken)
  {
    var component = await context.LoadComponentAsync(command.Id, cancellationToken);
    var i = command.Component;

    // the type decides which side of the slip a line falls on; it cannot flip once rules, loans or slips use it
    if (component.ComponentType != i.ComponentType && await IsComponentInUseAsync(component.Id, cancellationToken))
      return Result<UpdatedResult>.Failure($"{component.ComponentCode} is already used by rules, loans or pay slips; its type cannot change.");

    component.Update(i.ComponentName, i.ComponentType, i.IsTaxable);
    if (await context.SalaryComponents.AnyAsync(c => c.Id != component.Id && c.ComponentName.ToLower() == component.ComponentName.ToLower(), cancellationToken))
      return Result<UpdatedResult>.Failure($"Another component is named '{component.ComponentName}'.");

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(SetSalaryComponentActivationCommand command, CancellationToken cancellationToken)
  {
    var component = await context.LoadComponentAsync(command.Id, cancellationToken);
    if (!command.IsActive)
    {
      var today = clock.Today;
      if (await context.SalaryRules.AnyAsync(r => r.SalaryComponentId == component.Id && r.Status == RecordStatus.Active
          && (r.EffectiveTo == null || r.EffectiveTo >= today), cancellationToken))
        return Result<UpdatedResult>.Failure($"{component.ComponentCode} still has rules in force; close them first.");
      if (await context.LoanTypes.AnyAsync(t => t.SalaryComponentId == component.Id && t.IsActive, cancellationToken))
        return Result<UpdatedResult>.Failure($"Active loan types recover their installments on {component.ComponentCode}.");
    }

    component.SetActive(command.IsActive);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  // ---- rules ----

  public async Task<Result<GetSalaryRulesQueryResult>> Handle(GetSalaryRulesQuery query, CancellationToken cancellationToken)
  {
    var rows = context.SalaryRules.AsNoTracking().Where(r => query.IncludeInactive || r.Status == RecordStatus.Active);
    if (query.ComponentId is { } component)
    {
      var componentId = SalaryComponentId.Of(component);
      rows = rows.Where(r => r.SalaryComponentId == componentId);
    }
    if (query.InForceOn is { } date)
      rows = rows.Where(r => r.EffectiveFrom <= date && (r.EffectiveTo == null || r.EffectiveTo >= date));

    var list = await rows.OrderBy(r => r.SalaryComponentId).ThenBy(r => r.Priority).ThenByDescending(r => r.EffectiveFrom).ToListAsync(cancellationToken);
    return Result<GetSalaryRulesQueryResult>.Success(new(await MapRulesAsync(list, cancellationToken)));
  }

  public async Task<Result<GetSalaryRuleQueryResult>> Handle(GetSalaryRuleQuery query, CancellationToken cancellationToken)
  {
    var rule = await LoadRuleAsync(query.Id, cancellationToken);
    return Result<GetSalaryRuleQueryResult>.Success(new((await MapRulesAsync([rule], cancellationToken))[0]));
  }

  public async Task<Result<CreatedResult>> Handle(CreateSalaryRuleCommand command, CancellationToken cancellationToken)
  {
    var component = await context.LoadComponentAsync(command.ComponentId, cancellationToken);
    await EnsureScopeExistsAsync(command.Rule, cancellationToken);
    var rule = SalaryComponentRule.Create(SalaryComponentRuleId.New(), component, command.Rule.ToDefinition(), currentUser.UserId);
    context.SalaryRules.Add(rule);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(rule.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(UpdateSalaryRuleCommand command, CancellationToken cancellationToken)
  {
    var rule = await LoadRuleAsync(command.Id, cancellationToken, tracked: true);
    if (await IsRuleSettledAsync(rule.Id, cancellationToken))
      return Result<UpdatedResult>.Failure($"Rule {rule.RuleVersion} is on approved or finalized pay slips; revise it (a new version from a date) instead of editing it.");

    var component = await context.SalaryComponents.FirstAsync(c => c.Id == rule.SalaryComponentId, cancellationToken);
    await EnsureScopeExistsAsync(command.Rule, cancellationToken);
    rule.Update(component, command.Rule.ToDefinition());
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<CreatedResult>> Handle(ReviseSalaryRuleCommand command, CancellationToken cancellationToken)
  {
    var current = await LoadRuleAsync(command.Id, cancellationToken, tracked: true);
    var from = command.Rule.EffectiveFrom;
    if (from <= current.EffectiveFrom)
      return Result<CreatedResult>.Failure($"The new version must start after {current.EffectiveFrom:yyyy-MM-dd}, when rule {current.RuleVersion} started.");
    if (current.EffectiveTo is { } to && from > to.AddDays(1))
      return Result<CreatedResult>.Failure($"Rule {current.RuleVersion} already ended on {to:yyyy-MM-dd}; add a new rule instead.");

    var component = await context.SalaryComponents.FirstAsync(c => c.Id == current.SalaryComponentId, cancellationToken);
    await EnsureScopeExistsAsync(command.Rule, cancellationToken);
    current.CloseOn(from.AddDays(-1));
    var next = SalaryComponentRule.Create(SalaryComponentRuleId.New(), component, command.Rule.ToDefinition(), currentUser.UserId);
    context.SalaryRules.Add(next);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(next.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(CloseSalaryRuleCommand command, CancellationToken cancellationToken)
  {
    var rule = await LoadRuleAsync(command.Id, cancellationToken, tracked: true);
    rule.CloseOn(command.LastDay);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(SetSalaryRuleStatusCommand command, CancellationToken cancellationToken)
  {
    var rule = await LoadRuleAsync(command.Id, cancellationToken, tracked: true);
    rule.SetStatus(command.IsActive ? RecordStatus.Active : RecordStatus.Inactive);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<TrySalaryRuleResult>> Handle(TrySalaryRuleQuery query, CancellationToken cancellationToken)
  {
    var componentId = SalaryComponentId.Of(query.ComponentId);
    var component = await context.SalaryComponents.AsNoTracking().FirstOrDefaultAsync(c => c.Id == componentId, cancellationToken)
      ?? throw new SalaryComponentNotFoundException($"Salary component {query.ComponentId} was not found.");
    var rule = SalaryComponentRule.Create(SalaryComponentRuleId.New(), component, query.Rule.ToDefinition(), currentUser.UserId);
    var figures = query.Figures.ToFigures();
    var amount = rule.Calculate(figures);
    var prorated = figures.DaysInPeriod == 0 ? 0 : decimal.Round(amount.Amount * figures.DaysPayable / figures.DaysInPeriod, 2, MidpointRounding.AwayFromZero);
    return Result<TrySalaryRuleResult>.Success(new(amount.Amount, amount.CalculationBase, amount.BaseAmount, amount.Rate, amount.FormulaReference, prorated));
  }

  // ---- employee overrides ----

  public async Task<Result<GetSalaryOverridesQueryResult>> Handle(GetSalaryOverridesQuery query, CancellationToken cancellationToken)
  {
    var employeeId = EmployeeId.Of(query.EmployeeId);
    var today = clock.Today;
    var rows = await context.SalaryOverrides.AsNoTracking()
      .Where(o => o.EmployeeId == employeeId && (query.IncludeEnded || o.EffectiveTo == null || o.EffectiveTo >= today))
      .OrderByDescending(o => o.EffectiveFrom).ToListAsync(cancellationToken);

    var componentIds = rows.Select(o => o.SalaryComponentId).Distinct().ToList();
    var components = await context.SalaryComponents.AsNoTracking().Where(c => componentIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);
    var posts = await lookup.PostCodesAsync(rows.Select(o => o.PostId), cancellationToken);

    return Result<GetSalaryOverridesQueryResult>.Success(new(rows.Select(o => new SalaryOverrideDto(o.Id.Value, o.EmployeeId.Value, o.SalaryComponentId.Value,
      components[o.SalaryComponentId].ComponentCode, components[o.SalaryComponentId].ComponentName, o.PostId?.Value,
      o.PostId is null ? null : posts.GetValueOrDefault(o.PostId.Value), o.OverrideFixedAmount, o.OverridePercentage, o.EffectiveFrom, o.EffectiveTo, o.Remarks)).ToList()));
  }

  public async Task<Result<CreatedResult>> Handle(CreateSalaryOverrideCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.EmployeeId, cancellationToken);
    employee.EnsureInService();
    var component = await context.LoadComponentAsync(command.ComponentId, cancellationToken);
    PostId? postId = null;
    if (command.PostId is { } post)
      postId = (await context.LoadPostAsync(post, cancellationToken)).Id;

    var i = command.Override;
    var existing = await OverridesOfAsync(employee.Id, component.Id, cancellationToken);
    var item = EmployeeSalaryComponent.Create(SalaryOverrideId.New(), employee.Id, component, postId, i.OverrideFixedAmount, i.OverridePercentage,
      i.EffectiveFrom, i.EffectiveTo, i.Remarks, existing);
    context.SalaryOverrides.Add(item);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(item.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(UpdateSalaryOverrideCommand command, CancellationToken cancellationToken)
  {
    var item = await LoadOverrideAsync(command.Id, cancellationToken);
    if (await IsOverrideSettledAsync(item, cancellationToken))
      return Result<UpdatedResult>.Failure("The override is on approved or finalized pay slips; end it and add a new one from the date the amount changes.");

    var i = command.Override;
    item.Update(i.OverrideFixedAmount, i.OverridePercentage, i.EffectiveFrom, i.EffectiveTo, i.Remarks, await OverridesOfAsync(item.EmployeeId, item.SalaryComponentId, cancellationToken));
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(EndSalaryOverrideCommand command, CancellationToken cancellationToken)
  {
    var item = await LoadOverrideAsync(command.Id, cancellationToken);
    item.EndOn(command.LastDay);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(DeleteSalaryOverrideCommand command, CancellationToken cancellationToken)
  {
    var item = await LoadOverrideAsync(command.Id, cancellationToken);
    if (await IsOverrideSettledAsync(item, cancellationToken))
      return Result<UpdatedResult>.Failure("The override is on approved or finalized pay slips; end it instead.");

    context.SalaryOverrides.Remove(item);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  private async Task<SalaryComponentRule> LoadRuleAsync(Guid id, CancellationToken cancellationToken, bool tracked = false)
  {
    var ruleId = SalaryComponentRuleId.Of(id);
    var rules = tracked ? context.SalaryRules : context.SalaryRules.AsNoTracking();
    return await rules.FirstOrDefaultAsync(r => r.Id == ruleId, cancellationToken)
      ?? throw new SalaryRuleNotFoundException($"Salary rule {id} was not found.");
  }

  private async Task<EmployeeSalaryComponent> LoadOverrideAsync(Guid id, CancellationToken cancellationToken)
  {
    var overrideId = SalaryOverrideId.Of(id);
    return await context.SalaryOverrides.FirstOrDefaultAsync(o => o.Id == overrideId, cancellationToken)
      ?? throw new SalaryOverrideNotFoundException($"Salary override {id} was not found.");
  }

  private Task<List<EmployeeSalaryComponent>> OverridesOfAsync(EmployeeId employeeId, SalaryComponentId componentId, CancellationToken cancellationToken) =>
      context.SalaryOverrides.AsNoTracking().Where(o => o.EmployeeId == employeeId && o.SalaryComponentId == componentId).ToListAsync(cancellationToken);

  private async Task EnsureScopeExistsAsync(SalaryRuleInput rule, CancellationToken cancellationToken)
  {
    if (rule.ApplicableDesignationId is { } designation)
      await context.LoadDesignationAsync(designation, cancellationToken);
    if (rule.ApplicableOrgUnitId is { } unit)
      await context.LoadOrgUnitAsync(unit, cancellationToken);
  }

  private Task<bool> IsRuleSettledAsync(SalaryComponentRuleId ruleId, CancellationToken cancellationToken) =>
      context.PayrollLines.AnyAsync(l => l.SalaryComponentRuleId == ruleId
        && context.PayrollTransactions.Any(t => t.Id == l.PayrollTransactionId
          && context.PayrollRuns.Any(r => r.Id == t.PayrollRunId && Settled.Contains(r.Status))), cancellationToken);

  /// An override counts as paid when a settled slip of a month it covers carries an override line of its component.
  private Task<bool> IsOverrideSettledAsync(EmployeeSalaryComponent item, CancellationToken cancellationToken)
  {
    var to = item.EffectiveTo ?? DateOnly.MaxValue;
    return context.PayrollLines.AnyAsync(l => l.SalaryComponentId == item.SalaryComponentId && l.Source == ComponentSource.Override
      && context.PayrollTransactions.Any(t => t.Id == l.PayrollTransactionId && t.EmployeeId == item.EmployeeId
        && context.PayrollRuns.Any(r => r.Id == t.PayrollRunId && Settled.Contains(r.Status)
          && context.PayrollPeriods.Any(p => p.Id == r.PayrollPeriodId && p.StartDate <= to && p.EndDate >= item.EffectiveFrom))), cancellationToken);
  }

  private async Task<bool> IsComponentInUseAsync(SalaryComponentId componentId, CancellationToken cancellationToken) =>
      await context.SalaryRules.AnyAsync(r => r.SalaryComponentId == componentId, cancellationToken)
      || await context.SalaryOverrides.AnyAsync(o => o.SalaryComponentId == componentId, cancellationToken)
      || await context.LoanTypes.AnyAsync(t => t.SalaryComponentId == componentId, cancellationToken)
      || await context.PayrollLines.AnyAsync(l => l.SalaryComponentId == componentId, cancellationToken);

  private async Task<List<SalaryRuleDto>> MapRulesAsync(List<SalaryComponentRule> rules, CancellationToken cancellationToken)
  {
    var componentIds = rules.Select(r => r.SalaryComponentId).Distinct().ToList();
    var components = await context.SalaryComponents.AsNoTracking().Where(c => componentIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);
    var designations = await lookup.DesignationsAsync(rules.Select(r => r.ApplicableDesignationId), cancellationToken);
    var units = await lookup.UnitNamesAsync(rules.Select(r => r.ApplicableOrgUnitId), clock.Today, cancellationToken);
    var ruleIds = rules.Select(r => r.Id).ToList();
    var used = (await context.PayrollLines.AsNoTracking()
        .Where(l => l.SalaryComponentRuleId != null && ruleIds.Contains(l.SalaryComponentRuleId)
          && context.PayrollTransactions.Any(t => t.Id == l.PayrollTransactionId
            && context.PayrollRuns.Any(r => r.Id == t.PayrollRunId && Settled.Contains(r.Status))))
        .Select(l => l.SalaryComponentRuleId!).Distinct().ToListAsync(cancellationToken))
      .ToHashSet();

    return rules.Select(r =>
    {
      var component = components[r.SalaryComponentId];
      return new SalaryRuleDto(r.Id.Value, r.SalaryComponentId.Value, component.ComponentCode, component.ComponentName, r.RuleVersion, r.CalculationMethod,
        r.FixedAmount, r.Percentage, r.CalculationBase, r.FormulaExpression, r.MinBps, r.MaxBps, r.ApplicableDesignationId?.Value,
        r.ApplicableDesignationId is null ? null : designations.GetValueOrDefault(r.ApplicableDesignationId.Value), r.ApplicableOrgUnitId?.Value,
        r.ApplicableOrgUnitId is null ? null : units.GetValueOrDefault(r.ApplicableOrgUnitId.Value), r.ApplicableEmploymentType, r.MinAmount, r.MaxAmount,
        r.Priority, r.NotificationRef, r.EffectiveFrom, r.EffectiveTo, r.Status, r.ApprovedBy, used.Contains(r.Id));
    }).ToList();
  }
}
