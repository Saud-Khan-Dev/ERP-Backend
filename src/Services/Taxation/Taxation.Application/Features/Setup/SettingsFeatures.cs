using FluentValidation;
using Microsoft.EntityFrameworkCore;

// ---- settings ----

public sealed record GetSettingsQueryResult(DateOnly AsOf, IReadOnlyList<SettingDto> Settings);
public sealed record GetSettingsQuery(DateOnly? AsOf) : IQuery<Result<GetSettingsQueryResult>>;

/// New values of some settings from a date (each key closes its value in force the day before).
public sealed record ChangeSettingsCommand(DateOnly EffectiveFrom, IReadOnlyDictionary<string, string> Values, Guid? RateSetId) : ICommand<Result<UpdatedResult>>;

/// Takes back a value entered from a later date by mistake, before it is in force; the value it would have ended runs on.
public sealed record VoidSettingCommand(string Key, DateOnly EffectiveFrom) : ICommand<Result<UpdatedResult>>;

/// Every value a setting has had and will have, newest first.
public sealed record GetSettingHistoryQueryResult(string Key, IReadOnlyList<SettingDto> Values);
public sealed record GetSettingHistoryQuery(string Key) : IQuery<Result<GetSettingHistoryQueryResult>>;

public class ChangeSettingsCommandValidator : AbstractValidator<ChangeSettingsCommand>
{
  public ChangeSettingsCommandValidator()
  {
    RuleFor(x => x.Values).NotEmpty().WithMessage("Give at least one setting.");
    RuleForEach(x => x.Values).Must(kv => kv.Value is not null && kv.Value.Length <= 100).WithMessage("A value has at most 100 characters.");
  }
}

public class SettingsHandlers(IApplicationDbContext context, IClock clock) :
  IQueryHandler<GetSettingsQuery, Result<GetSettingsQueryResult>>,
  ICommandHandler<ChangeSettingsCommand, Result<UpdatedResult>>,
  ICommandHandler<VoidSettingCommand, Result<UpdatedResult>>,
  IQueryHandler<GetSettingHistoryQuery, Result<GetSettingHistoryQueryResult>>
{
  public async Task<Result<UpdatedResult>> Handle(VoidSettingCommand command, CancellationToken cancellationToken)
  {
    var key = SettingKeys.Find(command.Key).Key;
    var history = await context.Settings.Where(s => s.Key == key).ToListAsync(cancellationToken);
    var future = history.FirstOrDefault(s => s.EffectiveFrom == command.EffectiveFrom)
      ?? throw new TemplateNotFoundException($"{key} has no value from {command.EffectiveFrom:dd MMM yyyy}.");
    TaxSetting.Void(future, history, clock.Today);
    context.Settings.Remove(future);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<GetSettingHistoryQueryResult>> Handle(GetSettingHistoryQuery query, CancellationToken cancellationToken)
  {
    var definition = SettingKeys.Find(query.Key);
    var rows = await context.Settings.AsNoTracking().Where(s => s.Key == definition.Key).OrderByDescending(s => s.EffectiveFrom).ToListAsync(cancellationToken);
    return Result<GetSettingHistoryQueryResult>.Success(new(definition.Key, rows.Select(r =>
      new SettingDto(definition.Key, r.Value, definition.Default, definition.Description, r.EffectiveFrom, r.EffectiveTo, r.RateSetId?.Value)).ToList()));
  }

  public async Task<Result<GetSettingsQueryResult>> Handle(GetSettingsQuery query, CancellationToken cancellationToken)
  {
    var asOf = query.AsOf ?? clock.Today;
    var rows = await context.Settings.AsNoTracking().Where(s => s.EffectiveFrom <= asOf && (s.EffectiveTo == null || s.EffectiveTo >= asOf)).ToListAsync(cancellationToken);
    var list = SettingKeys.All.Select(d =>
    {
      var row = rows.Where(r => r.Key == d.Key).OrderByDescending(r => r.EffectiveFrom).FirstOrDefault();
      return new SettingDto(d.Key, row?.Value ?? d.Default, d.Default, d.Description, row?.EffectiveFrom, row?.EffectiveTo, row?.RateSetId?.Value);
    }).ToList();
    return Result<GetSettingsQueryResult>.Success(new(asOf, list));
  }

  public async Task<Result<UpdatedResult>> Handle(ChangeSettingsCommand command, CancellationToken cancellationToken)
  {
    RateSetId? rateSetId = command.RateSetId is { } rs ? (await context.LoadRateSetAsync(rs, cancellationToken)).Id : null;
    var all = await context.Settings.ToListAsync(cancellationToken);

    foreach (var (rawKey, value) in command.Values)
    {
      var key = SettingKeys.Find(rawKey).Key;
      var history = all.Where(s => s.Key == key).ToList();
      var current = history.Where(s => s.Covers(command.EffectiveFrom)).OrderByDescending(s => s.EffectiveFrom).FirstOrDefault();
      var created = TaxSetting.Change(current, key, value, command.EffectiveFrom, rateSetId, history);
      if (created is not null)
      {
        context.Settings.Add(created);
        all.Add(created);
      }
    }

    // the values in force from the date must fit together (the notice days rise)
    var inForce = all.Where(s => s.Covers(command.EffectiveFrom)).GroupBy(s => s.Key).ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.EffectiveFrom).First().Value);
    TaxSettings.From(inForce).EnsureConsistent();

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }
}

// ---- special policies ----

public sealed record SpecialPolicyRequest(Guid HeadId, SpecialPolicyInput Policy);

public sealed record GetSpecialPoliciesQueryResult(IReadOnlyList<SpecialPolicyDto> Policies);
public sealed record GetSpecialPoliciesQuery(Guid? HeadId, bool IncludeInactive) : IQuery<Result<GetSpecialPoliciesQueryResult>>;
public sealed record CreateSpecialPolicyCommand(Guid HeadId, SpecialPolicyInput Policy) : ICommand<Result<CreatedResult>>;
public sealed record UpdateSpecialPolicyCommand(Guid Id, Guid HeadId, SpecialPolicyInput Policy) : ICommand<Result<UpdatedResult>>;
public sealed record SetSpecialPolicyActivationCommand(Guid Id, bool IsActive) : ICommand<Result<UpdatedResult>>;

/// Deletes a policy made by mistake: only while no property has it and no demand was charged with it.
public sealed record DeleteSpecialPolicyCommand(Guid Id) : ICommand<Result<UpdatedResult>>;

public class SpecialPolicyInputValidator : AbstractValidator<SpecialPolicyInput>
{
  public SpecialPolicyInputValidator()
  {
    RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    RuleFor(x => x.Code).MaximumLength(30);
    RuleFor(x => x.Value).GreaterThan(0);
    RuleFor(x => x.BoardReference).MaximumLength(100);
  }
}

public class CreateSpecialPolicyCommandValidator : AbstractValidator<CreateSpecialPolicyCommand>
{
  public CreateSpecialPolicyCommandValidator() => RuleFor(x => x.Policy).NotNull().SetValidator(new SpecialPolicyInputValidator());
}

public class UpdateSpecialPolicyCommandValidator : AbstractValidator<UpdateSpecialPolicyCommand>
{
  public UpdateSpecialPolicyCommandValidator() => RuleFor(x => x.Policy).NotNull().SetValidator(new SpecialPolicyInputValidator());
}

public class SpecialPolicyHandlers(IApplicationDbContext context, IClock clock) :
  IQueryHandler<GetSpecialPoliciesQuery, Result<GetSpecialPoliciesQueryResult>>,
  ICommandHandler<CreateSpecialPolicyCommand, Result<CreatedResult>>,
  ICommandHandler<UpdateSpecialPolicyCommand, Result<UpdatedResult>>,
  ICommandHandler<SetSpecialPolicyActivationCommand, Result<UpdatedResult>>,
  ICommandHandler<DeleteSpecialPolicyCommand, Result<UpdatedResult>>
{
  public async Task<Result<UpdatedResult>> Handle(DeleteSpecialPolicyCommand command, CancellationToken cancellationToken)
  {
    var policy = await context.LoadPolicyAsync(command.Id, cancellationToken);
    if (await context.Overrides.AnyAsync(o => o.PolicyId == policy.Id, cancellationToken) || await context.DemandLines.AnyAsync(l => l.PolicyId == policy.Id, cancellationToken))
      return Result<UpdatedResult>.Failure($"\"{policy.Name}\" is assigned to properties or was charged on demands; deactivate it instead.");
    context.SpecialPolicies.Remove(policy);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<GetSpecialPoliciesQueryResult>> Handle(GetSpecialPoliciesQuery query, CancellationToken cancellationToken)
  {
    var rows = context.SpecialPolicies.AsNoTracking().Where(p => query.IncludeInactive || p.IsActive);
    if (query.HeadId is { } head)
    {
      var headId = TaxHeadId.Of(head);
      rows = rows.Where(p => p.HeadId == headId);
    }
    var policies = await rows.OrderBy(p => p.Name).ToListAsync(cancellationToken);
    var heads = await context.TaxHeads.AsNoTracking().ToDictionaryAsync(h => h.Id, cancellationToken);
    // "used by N files": the properties it is assigned to now
    var today = clock.Today;
    var ids = policies.Select(p => p.Id).ToList();
    var used = await context.Overrides.AsNoTracking().Where(o => o.PolicyId != null && ids.Contains(o.PolicyId) && (o.EffectiveTo == null || o.EffectiveTo >= today))
      .GroupBy(o => o.PolicyId).Select(g => new { g.Key, Files = g.Select(o => o.TaxFileId).Distinct().Count() }).ToListAsync(cancellationToken);
    return Result<GetSpecialPoliciesQueryResult>.Success(new(policies.Select(p => p.ToDto(heads.GetValueOrDefault(p.HeadId),
      used.FirstOrDefault(u => u.Key == p.Id)?.Files ?? 0)).ToList()));
  }

  public async Task<Result<CreatedResult>> Handle(CreateSpecialPolicyCommand command, CancellationToken cancellationToken)
  {
    var head = await context.LoadHeadAsync(command.HeadId, cancellationToken);
    var policy = SpecialPolicy.Create(SpecialPolicyId.New(), head, command.Policy);
    if (policy.Code is { } code && await context.SpecialPolicies.AnyAsync(p => p.Code == code, cancellationToken))
      return Result<CreatedResult>.Failure($"A policy with the code {code} already exists.");
    context.SpecialPolicies.Add(policy);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(policy.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(UpdateSpecialPolicyCommand command, CancellationToken cancellationToken)
  {
    var policy = await context.LoadPolicyAsync(command.Id, cancellationToken);
    var head = await context.LoadHeadAsync(command.HeadId, cancellationToken);
    if (policy.HeadId != head.Id && await context.Overrides.AnyAsync(o => o.PolicyId == policy.Id, cancellationToken))
      return Result<UpdatedResult>.Failure($"\"{policy.Name}\" is assigned to properties; its head cannot change.");
    policy.Update(head, command.Policy);
    if (policy.Code is { } code && await context.SpecialPolicies.AnyAsync(p => p.Id != policy.Id && p.Code == code, cancellationToken))
      return Result<UpdatedResult>.Failure($"Another policy has the code {code}.");
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(SetSpecialPolicyActivationCommand command, CancellationToken cancellationToken)
  {
    var policy = await context.LoadPolicyAsync(command.Id, cancellationToken);
    policy.SetActive(command.IsActive);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }
}

// ---- penalty rules ----

public sealed record GetPenaltyRulesQueryResult(IReadOnlyList<PenaltyRuleDto> Rules);
/// The rules in force today (with History: every version, newest first).
public sealed record GetPenaltyRulesQuery(bool History = false) : IQuery<Result<GetPenaltyRulesQueryResult>>;

/// A change to a rule from a date (today when not given): a new version starts and the one in force ends the day before;
/// surcharges already applied keep the version they were applied with. Dated the version's own first day, it corrects it.
public sealed record UpdatePenaltyRuleCommand(Guid Id, PenaltyRuleInput Rule, DateOnly? EffectiveFrom = null) : ICommand<Result<CreatedResult>>;

/// Takes back a version entered from a later date by mistake, before it is in force.
public sealed record VoidPenaltyRuleCommand(Guid Id) : ICommand<Result<UpdatedResult>>;

public class PenaltyRuleHandlers(IApplicationDbContext context, IClock clock) :
  IQueryHandler<GetPenaltyRulesQuery, Result<GetPenaltyRulesQueryResult>>,
  ICommandHandler<UpdatePenaltyRuleCommand, Result<CreatedResult>>,
  ICommandHandler<VoidPenaltyRuleCommand, Result<UpdatedResult>>
{
  public async Task<Result<GetPenaltyRulesQueryResult>> Handle(GetPenaltyRulesQuery query, CancellationToken cancellationToken)
  {
    var today = clock.Today;
    var rules = await context.PenaltyRules.AsNoTracking().OrderBy(r => r.Kind).ThenBy(r => r.Code).ThenByDescending(r => r.EffectiveFrom).ToListAsync(cancellationToken);
    var shown = query.History ? rules
      : rules.GroupBy(r => r.Code).Select(g => g.FirstOrDefault(r => r.Covers(today)) ?? g.OrderBy(r => r.EffectiveFrom).First()).ToList();
    return Result<GetPenaltyRulesQueryResult>.Success(new(shown.Select(r => r.ToDto(today, rules.Any(v => v.Code == r.Code && v.EffectiveFrom > r.EffectiveFrom))).ToList()));
  }

  public async Task<Result<CreatedResult>> Handle(UpdatePenaltyRuleCommand command, CancellationToken cancellationToken)
  {
    var rule = await context.LoadRuleAsync(command.Id, cancellationToken);
    if (command.Rule.RateSetId is { } rs)
      await context.LoadRateSetAsync(rs, cancellationToken);
    var versions = await context.PenaltyRules.Where(r => r.Code == rule.Code).ToListAsync(cancellationToken);
    var next = rule.Revise(command.Rule, command.EffectiveFrom ?? clock.Today, versions);
    if (next is not null)
      context.PenaltyRules.Add(next);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(next?.Id ?? rule.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(VoidPenaltyRuleCommand command, CancellationToken cancellationToken)
  {
    var rule = await context.LoadRuleAsync(command.Id, cancellationToken);
    var versions = await context.PenaltyRules.Where(r => r.Code == rule.Code).ToListAsync(cancellationToken);
    PenaltyRule.Void(rule, versions, clock.Today);
    context.PenaltyRules.Remove(rule);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }
}

// ---- message templates ----

public sealed record GetMessageTemplatesQueryResult(IReadOnlyList<MessageTemplateDto> Templates);
public sealed record GetMessageTemplatesQuery : IQuery<Result<GetMessageTemplatesQueryResult>>;
public sealed record UpdateMessageTemplateCommand(Guid Id, string Body, bool IsActive) : ICommand<Result<UpdatedResult>>;

public class UpdateMessageTemplateCommandValidator : AbstractValidator<UpdateMessageTemplateCommand>
{
  public UpdateMessageTemplateCommandValidator() => RuleFor(x => x.Body).NotEmpty().MaximumLength(1000);
}

public class MessageTemplateHandlers(IApplicationDbContext context) :
  IQueryHandler<GetMessageTemplatesQuery, Result<GetMessageTemplatesQueryResult>>,
  ICommandHandler<UpdateMessageTemplateCommand, Result<UpdatedResult>>
{
  public async Task<Result<GetMessageTemplatesQueryResult>> Handle(GetMessageTemplatesQuery query, CancellationToken cancellationToken)
  {
    var templates = await context.MessageTemplates.AsNoTracking().OrderBy(t => t.Channel).ThenBy(t => t.Code).ToListAsync(cancellationToken);
    return Result<GetMessageTemplatesQueryResult>.Success(new(templates.Select(t => t.ToDto()).ToList()));
  }

  public async Task<Result<UpdatedResult>> Handle(UpdateMessageTemplateCommand command, CancellationToken cancellationToken)
  {
    var id = MessageTemplateId.Of(command.Id);
    var template = await context.MessageTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
      ?? throw new TemplateNotFoundException($"Message template {command.Id} was not found.");
    template.Change(command.Body, command.IsActive);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }
}
