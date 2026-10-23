using FluentValidation;
using Microsoft.EntityFrameworkCore;

/// One separation-of-duty rule as Administration › Configuration lists it: what it keeps apart, whether it is on, and
/// who switched it last and why (null: never switched - on since the start).
public sealed record DutyRuleDto(
  string Code,
  string Area,
  string Title,
  string Description,
  bool IsOn,
  string? ChangedByName,
  DateTime? ChangedAt,
  string? Reason);

public sealed record GetDutyRulesQuery : IQuery<Result<IReadOnlyList<DutyRuleDto>>>;

public class GetDutyRulesHandler(IApplicationDbContext context) : IQueryHandler<GetDutyRulesQuery, Result<IReadOnlyList<DutyRuleDto>>>
{
  public async Task<Result<IReadOnlyList<DutyRuleDto>>> Handle(GetDutyRulesQuery query, CancellationToken cancellationToken)
  {
    var switches = await context.DutyRuleSwitches.AsNoTracking().ToDictionaryAsync(r => r.Id, cancellationToken);
    var rules = DutyRules.All.Select(rule => switches.TryGetValue(rule.Code, out var s)
        ? new DutyRuleDto(rule.Code, rule.Area, rule.Title, rule.Description, s.IsOn, s.ChangedByName, s.ChangedAt, s.Reason)
        : new DutyRuleDto(rule.Code, rule.Area, rule.Title, rule.Description, true, null, null, null))
      .ToList();
    return Result<IReadOnlyList<DutyRuleDto>>.Success(rules);
  }
}

/// Every rule's code and whether it is on - what the other services read (no names, no reasons).
public sealed record GetDutyRuleStatesQuery : IQuery<Result<IReadOnlyList<DutyRuleStateDto>>>;

public class GetDutyRuleStatesHandler(IApplicationDbContext context) : IQueryHandler<GetDutyRuleStatesQuery, Result<IReadOnlyList<DutyRuleStateDto>>>
{
  public async Task<Result<IReadOnlyList<DutyRuleStateDto>>> Handle(GetDutyRuleStatesQuery query, CancellationToken cancellationToken)
  {
    var switches = await context.DutyRuleSwitches.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.IsOn, cancellationToken);
    return Result<IReadOnlyList<DutyRuleStateDto>>.Success(
      DutyRules.All.Select(r => new DutyRuleStateDto(r.Code, !switches.TryGetValue(r.Code, out var on) || on)).ToList());
  }
}

/// A Super Administrator switches a rule on or off, saying why. Switching a check OFF lets one person do both halves of a
/// piece of work, so it is never silent: the activity trail and the Overview show it until it is back on.
public sealed record SwitchDutyRuleCommand(string Code, bool IsOn, string Reason) : ICommand<Result<DutyRuleDto>>;

public class SwitchDutyRuleValidator : AbstractValidator<SwitchDutyRuleCommand>
{
  public SwitchDutyRuleValidator()
  {
    RuleFor(x => x.Code).Must(code => DutyRules.Find(code) is not null).WithMessage("There is no such rule.");
    RuleFor(x => x.Reason).NotEmpty().WithMessage("Say why the rule is being switched.").MinimumLength(5).MaximumLength(500);
  }
}

public class SwitchDutyRuleHandler(IApplicationDbContext context, IdentityGuard guard, ICurrentUser currentUser, IActivityRecorder activity)
  : ICommandHandler<SwitchDutyRuleCommand, Result<DutyRuleDto>>
{
  public async Task<Result<DutyRuleDto>> Handle(SwitchDutyRuleCommand command, CancellationToken cancellationToken)
  {
    if (!guard.CallerIsSuperAdmin)
      throw new DomainException("Only a Super Administrator may switch a separation-of-duty rule.");
    var rule = DutyRules.Find(command.Code)!;
    var now = DateTime.UtcNow;
    var name = currentUser.AuditName;

    var row = await context.DutyRuleSwitches.FirstOrDefaultAsync(r => r.Id == rule.Code, cancellationToken);
    var wasOn = row?.IsOn ?? true;
    if (wasOn == command.IsOn)
      return Result<DutyRuleDto>.Failure(command.IsOn ? "This rule is already on." : "This rule is already off.");
    if (row is null)
      await context.DutyRuleSwitches.AddAsync(DutyRuleSwitch.Create(rule.Code, command.IsOn, currentUser.UserId, name, command.Reason, now), cancellationToken);
    else
      row.Set(command.IsOn, currentUser.UserId, name, command.Reason, now);

    await activity.RecordAsync(ActivityAction.DutyRuleSwitched, ActivityTargetType.System, null, rule.Title,
      $"{(command.IsOn ? "Switched on" : "Switched off")} · {command.Reason.Trim()}", cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<DutyRuleDto>.Success(new DutyRuleDto(rule.Code, rule.Area, rule.Title, rule.Description, command.IsOn, name, now, command.Reason.Trim()));
  }
}
