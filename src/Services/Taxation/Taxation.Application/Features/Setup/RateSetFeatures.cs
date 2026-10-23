using FluentValidation;
using Microsoft.EntityFrameworkCore;

/// BaseFiscalYear: the year the rates are stated for (the annual increase compounds from it); empty = the first year the
/// set is used for (the year of the first 1 July on or after its date).
public sealed record RateSetInput(string Name, string BoardReference, DateOnly? BoardDecisionDate, DateOnly EffectiveFrom, int? BaseFiscalYear = null);

public sealed record GetRateSetsQueryResult(IReadOnlyList<RateSetSummaryDto> RateSets);
public sealed record GetRateSetsQuery : IQuery<Result<GetRateSetsQueryResult>>;
public sealed record GetRateSetQueryResult(RateSetDto RateSet);
public sealed record GetRateSetQuery(Guid Id) : IQuery<Result<GetRateSetQueryResult>>;

/// A new draft, empty or starting from another set's factors and rates.
public sealed record CreateRateSetCommand(RateSetInput RateSet, Guid? CopyFromId) : ICommand<Result<CreatedResult>>;
public sealed record UpdateRateSetCommand(Guid Id, RateSetInput RateSet) : ICommand<Result<UpdatedResult>>;
public sealed record DeleteRateSetCommand(Guid Id) : ICommand<Result<UpdatedResult>>;

/// A town's locality factors for one category and component, as bands (upTo null = "and above").
public sealed record SetLtfFactorsCommand(Guid Id, Guid TownId, TaxCategory TaxCategory, LtfComponent Component, IReadOnlyList<BandInput> Bands) : ICommand<Result<UpdatedResult>>;
public sealed record RemoveTownFactorsCommand(Guid Id, Guid TownId) : ICommand<Result<UpdatedResult>>;

/// A head's rates for one category (null = both, for fees) and town (null = all towns).
public sealed record SetHeadRatesCommand(Guid Id, Guid HeadId, TaxCategory? TaxCategory, Guid? TownId, HeadRateInput Rates) : ICommand<Result<UpdatedResult>>;
public sealed record RemoveHeadRatesCommand(Guid Id, Guid HeadId, TaxCategory? TaxCategory, Guid? TownId) : ICommand<Result<UpdatedResult>>;

/// Approves a draft: it takes effect from its date and the set it replaces ends the day before.
public sealed record ApproveRateSetCommand(Guid Id) : ICommand<Result<UpdatedResult>>;

public class RateSetInputValidator : AbstractValidator<RateSetInput>
{
  public RateSetInputValidator()
  {
    RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    RuleFor(x => x.BoardReference).NotEmpty().MaximumLength(100);
  }
}

public class CreateRateSetCommandValidator : AbstractValidator<CreateRateSetCommand>
{
  public CreateRateSetCommandValidator() => RuleFor(x => x.RateSet).NotNull().SetValidator(new RateSetInputValidator());
}

public class UpdateRateSetCommandValidator : AbstractValidator<UpdateRateSetCommand>
{
  public UpdateRateSetCommandValidator() => RuleFor(x => x.RateSet).NotNull().SetValidator(new RateSetInputValidator());
}

public class SetLtfFactorsCommandValidator : AbstractValidator<SetLtfFactorsCommand>
{
  public SetLtfFactorsCommandValidator()
  {
    RuleFor(x => x.TownId).NotEmpty();
    RuleFor(x => x.Bands).NotNull();
  }
}

public class SetHeadRatesCommandValidator : AbstractValidator<SetHeadRatesCommand>
{
  public SetHeadRatesCommandValidator()
  {
    RuleFor(x => x.HeadId).NotEmpty();
    RuleFor(x => x.Rates).NotNull();
    RuleFor(x => x.Rates.UnitLabel).MaximumLength(60);
  }
}

/// A rate set's History: its own changes and its factors' and rates' (the Board's tariff as it was drafted and approved).
public sealed record GetRateSetHistoryQuery(Guid Id, AuditFilter Filter) : IQuery<Result<AuditResponse>>;

public class RateSetHandlers(IApplicationDbContext context, IPropertyDirectory property, ICurrentUser currentUser, IDutyRules dutyRules, IAuditTrail audit, IClock clock) :
  IQueryHandler<GetRateSetHistoryQuery, Result<AuditResponse>>,
  IQueryHandler<GetRateSetsQuery, Result<GetRateSetsQueryResult>>,
  IQueryHandler<GetRateSetQuery, Result<GetRateSetQueryResult>>,
  ICommandHandler<CreateRateSetCommand, Result<CreatedResult>>,
  ICommandHandler<UpdateRateSetCommand, Result<UpdatedResult>>,
  ICommandHandler<DeleteRateSetCommand, Result<UpdatedResult>>,
  ICommandHandler<SetLtfFactorsCommand, Result<UpdatedResult>>,
  ICommandHandler<RemoveTownFactorsCommand, Result<UpdatedResult>>,
  ICommandHandler<SetHeadRatesCommand, Result<UpdatedResult>>,
  ICommandHandler<RemoveHeadRatesCommand, Result<UpdatedResult>>,
  ICommandHandler<ApproveRateSetCommand, Result<UpdatedResult>>
{
  public async Task<Result<GetRateSetsQueryResult>> Handle(GetRateSetsQuery query, CancellationToken cancellationToken)
  {
    var sets = await context.RateSets.AsNoTracking().Include(s => s.LtfFactors).Include(s => s.RateBands).AsSplitQuery()
      .OrderByDescending(s => s.EffectiveFrom).ToListAsync(cancellationToken);
    return Result<GetRateSetsQueryResult>.Success(new(sets.Select(s => s.ToSummary()).ToList()));
  }

  public async Task<Result<GetRateSetQueryResult>> Handle(GetRateSetQuery query, CancellationToken cancellationToken)
  {
    var set = await context.LoadRateSetAsync(query.Id, cancellationToken);
    var heads = await context.TaxHeads.AsNoTracking().ToDictionaryAsync(h => h.Id, cancellationToken);
    var towns = (await context.CircleTowns.AsNoTracking().ToListAsync(cancellationToken))
      .GroupBy(t => t.TownId).ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.EffectiveFrom).First().TownCode);
    return Result<GetRateSetQueryResult>.Success(new(set.ToDto(heads, towns)));
  }

  public async Task<Result<CreatedResult>> Handle(CreateRateSetCommand command, CancellationToken cancellationToken)
  {
    var r = command.RateSet;
    var set = RateSet.Create(RateSetId.New(), r.Name, r.BoardReference, r.BoardDecisionDate, r.EffectiveFrom, r.BaseFiscalYear);
    if (command.CopyFromId is { } sourceId)
      set.CopyRatesFrom(await context.LoadRateSetAsync(sourceId, cancellationToken));
    context.RateSets.Add(set);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(set.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(UpdateRateSetCommand command, CancellationToken cancellationToken)
  {
    var set = await context.LoadRateSetAsync(command.Id, cancellationToken);
    var r = command.RateSet;
    set.UpdateDetails(r.Name, r.BoardReference, r.BoardDecisionDate, r.EffectiveFrom, r.BaseFiscalYear);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(DeleteRateSetCommand command, CancellationToken cancellationToken)
  {
    var set = await context.LoadRateSetAsync(command.Id, cancellationToken);
    if (set.Status != RateSetStatus.Draft)
      return Result<UpdatedResult>.Failure($"'{set.Name}' is approved; approved rates are kept.");
    context.RateSets.Remove(set);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(SetLtfFactorsCommand command, CancellationToken cancellationToken)
  {
    var set = await context.LoadRateSetAsync(command.Id, cancellationToken);
    if (!await context.CircleTowns.AnyAsync(t => t.TownId == command.TownId, cancellationToken) && await property.TownAsync(command.TownId, cancellationToken) is null)
      throw new DomainException("Property has no such town.");
    set.SetLtfFactors(command.TownId, command.TaxCategory, command.Component, command.Bands);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(RemoveTownFactorsCommand command, CancellationToken cancellationToken)
  {
    var set = await context.LoadRateSetAsync(command.Id, cancellationToken);
    set.RemoveTownFactors(command.TownId);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(SetHeadRatesCommand command, CancellationToken cancellationToken)
  {
    var set = await context.LoadRateSetAsync(command.Id, cancellationToken);
    var head = await context.LoadHeadAsync(command.HeadId, cancellationToken);
    set.SetHeadRates(head, command.TaxCategory, command.TownId, command.Rates);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(RemoveHeadRatesCommand command, CancellationToken cancellationToken)
  {
    var set = await context.LoadRateSetAsync(command.Id, cancellationToken);
    set.RemoveHeadRates(TaxHeadId.Of(command.HeadId), command.TaxCategory, command.TownId);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(ApproveRateSetCommand command, CancellationToken cancellationToken)
  {
    var set = await context.LoadRateSetAsync(command.Id, cancellationToken);
    var heads = await context.TaxHeads.Where(h => h.HeadKind == HeadKind.Regular).ToListAsync(cancellationToken);
    var towns = (await context.CircleTowns.Where(t => t.EffectiveTo == null || t.EffectiveTo >= set.EffectiveFrom).ToListAsync(cancellationToken))
      .GroupBy(t => t.TownId).Select(g => (g.Key, g.First().TownCode)).ToList();
    var approved = await context.RateSets.Where(s => s.Status != RateSetStatus.Draft && s.Id != set.Id).ToListAsync(cancellationToken);
    var actor = Guard.Actor(currentUser.UserId, "approve a rate set");
    if (set.Status != RateSetStatus.Draft)
      return Result<UpdatedResult>.Failure($"'{set.Name}' is already approved.");

    // the tariff passes through two people: whoever drafted any part of it (the set, its factors, its rates) does not approve it
    var setId = set.Id.Value;
    var drafters = new HashSet<Guid?> { set.CreatedBy, set.UpdatedBy };
    drafters.UnionWith(set.LtfFactors.Select(f => f.CreatedBy));
    drafters.UnionWith(set.RateBands.Select(b => b.CreatedBy));
    drafters.UnionWith(await context.AuditTrail.Where(x => (x.ParentId == setId || x.RecordId == setId) && x.ActorId != null).Select(x => x.ActorId).Distinct()
      .ToListAsync(cancellationToken));
    if (drafters.Contains(actor)
        && await dutyRules.SeparationRefusalAsync(DutyRules.TaxRateSet, actor, actor, "approve a rate set you drafted", cancellationToken) is { } refusal)
      return Result<UpdatedResult>.Failure(refusal);

    set.Approve(actor, clock.UtcNow, heads, towns, approved);
    audit.Record("Approved", set, $"In force from {set.EffectiveFrom:dd MMM yyyy} ({set.BoardReference}); {set.LtfFactors.Select(f => f.TownId).Distinct().Count()} town(s) with factors, "
      + $"{set.RateBands.Select(b => b.HeadId).Distinct().Count()} head(s) with rates");
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<AuditResponse>> Handle(GetRateSetHistoryQuery query, CancellationToken cancellationToken)
  {
    var set = await context.RateSets.AsNoTracking().FirstOrDefaultAsync(s => s.Id == RateSetId.Of(query.Id), cancellationToken)
      ?? throw new RateSetNotFoundException($"Rate set {query.Id} was not found.");
    var id = set.Id.Value;
    var trail = context.AuditTrail.Where(x => x.ParentId == id || x.RecordId == id);
    return Result<AuditResponse>.Success(new(await AuditQueries.ListAsync(trail, query.Filter with { ParentId = null, RecordId = null }, cancellationToken)));
  }
}
