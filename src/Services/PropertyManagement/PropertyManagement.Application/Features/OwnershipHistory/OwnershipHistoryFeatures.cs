using FluentValidation;
using Microsoft.EntityFrameworkCore;

/// One owner's share in a step: given (a giver) or received (a receiver, or a first owner).
public sealed record HistoryShareInput(Guid OwnerId, decimal SharePct);

/// One step, oldest first. Step 1 (FirstOwners): Receivers are the first owners with their shares (100% together);
/// TransferTypeId may say how they got it (First Registration, Allotment ...). Every later step (Transfer): Givers pass
/// shares to Receivers; TransferTypeId required. Date: the day, or the first day of the month / year (Precision Month /
/// Year), or roughly (About). ReferenceNo: the deed / mutation no.
public sealed record HistoryStepInput(
  HistoryStepKind Kind,
  DateOnly Date,
  DatePrecision Precision = DatePrecision.Exact,
  Guid? TransferTypeId = null,
  string? ReferenceNo = null,
  string? Relationship = null,
  IReadOnlyList<HistoryShareInput>? Givers = null,
  IReadOnlyList<HistoryShareInput>? Receivers = null)
{
  public HistoryStep ToStep() => new(Kind, Date, Precision, TransferTypeId, ReferenceNo, Relationship,
    (Givers ?? []).Select(g => new HistoryShare(g.OwnerId, g.SharePct)).ToList(),
    (Receivers ?? []).Select(r => new HistoryShare(r.OwnerId, r.SharePct)).ToList());
}

public sealed record HistoryEntryResult(Guid Id, string EntryNo, HistoryEntryStatus Status, IReadOnlyList<string> TransferNos);

public sealed record HistoryShareDto(OwnerRef? Owner, decimal SharePct);
public sealed record HistoryStepDto(int StepNo, HistoryStepKind Kind, DateOnly Date, DatePrecision Precision, MasterRef? TransferType, string? ReferenceNo,
  string? Relationship, IReadOnlyList<HistoryShareDto> Givers, IReadOnlyList<HistoryShareDto> Receivers);
/// CreatedBy / LastEditedByUserId: who entered and who last changed it - neither may register it (PROPERTY_TRANSFER).
public sealed record HistoryEntryDto(Guid Id, string EntryNo, PropertyRef? Property, HistoryEntryStatus Status, IReadOnlyList<HistoryStepDto> Steps,
  string? Remarks, string RecordedBy, DateTime? CreatedAt, Guid? CreatedBy, Guid? LastEditedByUserId, string? DecidedBy, DateOnly? DecidedOn, string? DecisionReason);
/// CanEnter: nobody is on the register, no allotment is in force and no history waits - a history may be entered.
public sealed record PropertyHistoryResult(IReadOnlyList<HistoryEntryDto> Entries, bool CanEnter, string? WhyNot);
public sealed record WaitingHistoriesResult(IReadOnlyList<HistoryEntryDto> Entries);

public sealed record RecordHistoryCommand(Guid PropertyId, IReadOnlyList<HistoryStepInput> Steps, string? Remarks) : ICommand<Result<HistoryEntryResult>>;
public sealed record ReviseHistoryCommand(Guid Id, IReadOnlyList<HistoryStepInput> Steps, string? Remarks) : ICommand<Result<HistoryEntryResult>>;
public sealed record RegisterHistoryCommand(Guid Id) : ICommand<Result<HistoryEntryResult>>;
public sealed record RejectHistoryCommand(Guid Id, string Reason) : ICommand<Result<HistoryEntryResult>>;
public sealed record WithdrawHistoryCommand(Guid Id, string Reason) : ICommand<Result<HistoryEntryResult>>;
public sealed record GetPropertyHistoryQuery(Guid PropertyId) : IQuery<Result<PropertyHistoryResult>>;
public sealed record GetWaitingHistoriesQuery : IQuery<Result<WaitingHistoriesResult>>;

public class HistoryStepInputValidator : AbstractValidator<HistoryStepInput>
{
  public HistoryStepInputValidator()
  {
    RuleFor(x => x.Kind).IsInEnum();
    RuleFor(x => x.Precision).IsInEnum();
    RuleFor(x => x.Date).NotEmpty().WithMessage("Give the date of the step (or its month or year).");
    RuleFor(x => x.ReferenceNo).MaximumLength(100);
    RuleFor(x => x.Relationship).MaximumLength(100);
    RuleForEach(x => x.Givers).ChildRules(s => { s.RuleFor(v => v.OwnerId).NotEmpty().WithMessage("Choose who gave the share."); s.RuleFor(v => v.SharePct).GreaterThan(0).LessThanOrEqualTo(100); });
    RuleForEach(x => x.Receivers).ChildRules(s => { s.RuleFor(v => v.OwnerId).NotEmpty().WithMessage("Choose who received the share."); s.RuleFor(v => v.SharePct).GreaterThan(0).LessThanOrEqualTo(100); });
  }
}

public class RecordHistoryCommandValidator : AbstractValidator<RecordHistoryCommand>
{
  public RecordHistoryCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.Steps).NotEmpty().WithMessage("Give the first owners on record, then each transfer.");
    RuleForEach(x => x.Steps).SetValidator(new HistoryStepInputValidator());
    RuleFor(x => x.Remarks).MaximumLength(4000);
  }
}

public class ReviseHistoryCommandValidator : AbstractValidator<ReviseHistoryCommand>
{
  public ReviseHistoryCommandValidator()
  {
    RuleFor(x => x.Steps).NotEmpty().WithMessage("Give the first owners on record, then each transfer.");
    RuleForEach(x => x.Steps).SetValidator(new HistoryStepInputValidator());
    RuleFor(x => x.Remarks).MaximumLength(4000);
  }
}

public class RejectHistoryCommandValidator : AbstractValidator<RejectHistoryCommand>
{
  public RejectHistoryCommandValidator() => RuleFor(x => x.Reason).NotEmpty().WithMessage("Say what is wrong, so the clerk can put it right.").MaximumLength(1000);
}

public class WithdrawHistoryCommandValidator : AbstractValidator<WithdrawHistoryCommand>
{
  public WithdrawHistoryCommandValidator() => RuleFor(x => x.Reason).NotEmpty().WithMessage("Say why it is withdrawn.").MaximumLength(1000);
}

/// Loads what a history is played against and reads entries back.
public class OwnershipHistoryService(IApplicationDbContext context, MasterLookup masters, ICurrentUser currentUser)
{
  public async Task<OwnershipHistoryEntry> LoadAsync(Guid id, CancellationToken cancellationToken) =>
      await context.HistoryEntries.FirstOrDefaultAsync(h => h.Id == HistoryEntryId.Of(id), cancellationToken)
      ?? throw new HistoryEntryNotFoundException($"Ownership history {id} was not found.");

  public async Task<HistoryContext> ContextAsync(Property property, IReadOnlyList<HistoryStep> steps, CancellationToken cancellationToken)
  {
    var ownerIds = steps.SelectMany(s => s.Givers.Concat(s.Receivers)).Select(x => x.OwnerId).Where(g => g != Guid.Empty).Distinct().Select(OwnerId.Of).ToList();
    var owners = await context.Owners.Where(o => ownerIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, cancellationToken);
    var typeIds = steps.Select(s => s.TransferTypeId).OfType<Guid>().Distinct().ToList();
    var types = new Dictionary<MasterId, TransferType>();
    foreach (var id in typeIds)
      types[MasterId.Of(id)] = await masters.GetAsync<TransferType>(id, cancellationToken);
    var tenure = await masters.GetByCodeAsync<TenureType>(SystemMasterCodes.TenureOwned, cancellationToken);
    return new HistoryContext(property, owners, types, tenure, TransferRegistration.Approver(currentUser), TransferRegistration.ActorName(currentUser), PakistanDate.Today);
  }

  /// Why a history cannot be entered on the property now; null when it can. Someone on the register (now or before) or an
  /// allotment in force rules it out.
  public async Task<string?> WhyNotAsync(Property property, Guid? except, CancellationToken cancellationToken)
  {
    if (await context.Ownerships.AnyAsync(o => o.PropertyId == property.Id, cancellationToken))
      return $"{property.PropertyCode.Value} already has owners on the register; its history from the old records is entered only while nobody is on record.";
    if (await context.AllotmentInForceAsync(property.Id, except: null, cancellationToken) is { } allotted)
      return $"{property.PropertyCode.Value} is allotted to {allotted.AllotteeName} ({allotted.Allotment.AllotmentNo.Value}).";
    var waiting = await context.HistoryEntries.AsNoTracking()
      .Where(h => h.PropertyId == property.Id && h.Status == HistoryEntryStatus.Waiting && (except == null || h.Id != HistoryEntryId.Of(except.Value)))
      .Select(h => h.EntryNo).FirstOrDefaultAsync(cancellationToken);
    return waiting is null ? null : $"{waiting.Value} already waits for this property: decide or withdraw it first.";
  }

  public async Task<IReadOnlyList<HistoryEntryDto>> ToDtosAsync(IReadOnlyList<OwnershipHistoryEntry> entries, CancellationToken cancellationToken)
  {
    var ownerIds = entries.SelectMany(e => e.Steps).SelectMany(s => s.Givers.Concat(s.Receivers)).Select(x => OwnerId.Of(x.OwnerId)).Distinct().ToList();
    var owners = await context.Owners.AsNoTracking().Where(o => ownerIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id.Value, o => o.ToRef(), cancellationToken);
    var propertyIds = entries.Select(e => e.PropertyId).Distinct().ToList();
    var properties = await context.Properties.AsNoTracking().Where(p => propertyIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.ToRef(), cancellationToken);
    var refs = await masters.Refs().Add<TransferType>(entries.SelectMany(e => e.Steps).Select(s => s.TransferTypeId).OfType<Guid>().Select(MasterId.Of)).LoadAsync(cancellationToken);
    HistoryShareDto Share(HistoryShare s) => new(owners.GetValueOrDefault(s.OwnerId), s.SharePct);
    return entries.Select(e => new HistoryEntryDto(
      e.Id.Value, e.EntryNo.Value, properties.GetValueOrDefault(e.PropertyId), e.Status,
      e.Steps.Select((s, i) => new HistoryStepDto(i + 1, s.Kind, s.Date, s.Precision, s.TransferTypeId is { } t ? refs[MasterId.Of(t)] : null, s.ReferenceNo,
        s.Relationship, s.Givers.Select(Share).ToList(), s.Receivers.Select(Share).ToList())).ToList(),
      e.Remarks, e.RecordedBy, e.CreatedAt, e.CreatedBy, e.LastEditedByUserId, e.DecidedBy, e.DecidedOn, e.DecisionReason)).ToList();
  }
}

public class OwnershipHistoryHandlers(
    IApplicationDbContext context, OwnershipHistoryService history, CodeGenerator codes, ICurrentUser currentUser, IDutyRules dutyRules, IAuditTrail audit)
  : ICommandHandler<RecordHistoryCommand, Result<HistoryEntryResult>>,
    ICommandHandler<ReviseHistoryCommand, Result<HistoryEntryResult>>,
    ICommandHandler<RegisterHistoryCommand, Result<HistoryEntryResult>>,
    ICommandHandler<RejectHistoryCommand, Result<HistoryEntryResult>>,
    ICommandHandler<WithdrawHistoryCommand, Result<HistoryEntryResult>>,
    IQueryHandler<GetPropertyHistoryQuery, Result<PropertyHistoryResult>>,
    IQueryHandler<GetWaitingHistoriesQuery, Result<WaitingHistoriesResult>>
{
  public async Task<Result<HistoryEntryResult>> Handle(RecordHistoryCommand command, CancellationToken cancellationToken)
  {
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);
    if (await history.WhyNotAsync(property, null, cancellationToken) is { } whyNot)
      throw new DomainException(whyNot);
    var steps = command.Steps.Select(s => s.ToStep()).ToList();
    var entry = OwnershipHistoryEntry.Record(HistoryEntryId.New(), await codes.NextAsync(CodeSequenceKeys.History, cancellationToken), steps, command.Remarks,
      currentUser.UserId, await history.ContextAsync(property, steps, cancellationToken), propertyHeldBefore: false);
    await context.HistoryEntries.AddAsync(entry, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return Result<HistoryEntryResult>.Success(new HistoryEntryResult(entry.Id.Value, entry.EntryNo.Value, entry.Status, []));
  }

  public async Task<Result<HistoryEntryResult>> Handle(ReviseHistoryCommand command, CancellationToken cancellationToken)
  {
    var entry = await history.LoadAsync(command.Id, cancellationToken);
    var property = await context.LoadPropertyAsync(entry.PropertyId.Value, cancellationToken);
    if (await history.WhyNotAsync(property, entry.Id.Value, cancellationToken) is { } whyNot)
      throw new DomainException(whyNot);
    var steps = command.Steps.Select(s => s.ToStep()).ToList();
    entry.Revise(steps, command.Remarks, currentUser.UserId, await history.ContextAsync(property, steps, cancellationToken), propertyHeldBefore: false);
    await context.SaveChangesAsync(cancellationToken);
    return Result<HistoryEntryResult>.Success(new HistoryEntryResult(entry.Id.Value, entry.EntryNo.Value, entry.Status, []));
  }

  public async Task<Result<HistoryEntryResult>> Handle(RegisterHistoryCommand command, CancellationToken cancellationToken)
  {
    var entry = await history.LoadAsync(command.Id, cancellationToken);
    // the PROPERTY_TRANSFER rule: neither who entered it nor who last changed it registers it
    foreach (var (maker, act) in new[] { (entry.CreatedBy, "entered"), (entry.LastEditedByUserId, "changed") })
      if (await dutyRules.SeparationRefusalAsync(DutyRules.PropertyTransfer, maker, currentUser.UserId, $"register {entry.EntryNo.Value}, which you {act}", cancellationToken) is { } refusal)
        throw new DomainException(refusal);

    var property = await context.LoadPropertyAsync(entry.PropertyId.Value, cancellationToken);
    var held = await context.Ownerships.AnyAsync(o => o.PropertyId == property.Id, cancellationToken)
      || await context.AllotmentInForceAsync(property.Id, except: null, cancellationToken) is not null;
    var numbers = new List<BusinessCode>();
    for (var i = 1; i < entry.Steps.Count; i++)
      numbers.Add(await codes.NextAsync(CodeSequenceKeys.Transfer, cancellationToken));

    var registration = entry.Register(await history.ContextAsync(property, entry.Steps, cancellationToken), held, n => numbers[n - 1]);
    await context.Transfers.AddRangeAsync(registration.Transfers, cancellationToken);
    await context.Ownerships.AddRangeAsync(registration.Rows, cancellationToken);
    audit.Record(PropertyAuditActions.Registered, entry, $"{registration.Transfers.Count} transfer(s) from the old records");
    await context.SaveChangesAsync(cancellationToken);
    return Result<HistoryEntryResult>.Success(new HistoryEntryResult(entry.Id.Value, entry.EntryNo.Value, entry.Status,
      registration.Transfers.Select(t => t.TransferNo.Value).ToList()));
  }

  public async Task<Result<HistoryEntryResult>> Handle(RejectHistoryCommand command, CancellationToken cancellationToken)
  {
    var entry = await history.LoadAsync(command.Id, cancellationToken);
    entry.Reject(TransferRegistration.Approver(currentUser), PakistanDate.Today, command.Reason);
    audit.Record(PropertyAuditActions.Rejected, entry, command.Reason);
    await context.SaveChangesAsync(cancellationToken);
    return Result<HistoryEntryResult>.Success(new HistoryEntryResult(entry.Id.Value, entry.EntryNo.Value, entry.Status, []));
  }

  public async Task<Result<HistoryEntryResult>> Handle(WithdrawHistoryCommand command, CancellationToken cancellationToken)
  {
    var entry = await history.LoadAsync(command.Id, cancellationToken);
    entry.Withdraw(TransferRegistration.Approver(currentUser), PakistanDate.Today, command.Reason);
    audit.Record(PropertyAuditActions.Cancelled, entry, command.Reason);
    await context.SaveChangesAsync(cancellationToken);
    return Result<HistoryEntryResult>.Success(new HistoryEntryResult(entry.Id.Value, entry.EntryNo.Value, entry.Status, []));
  }

  public async Task<Result<PropertyHistoryResult>> Handle(GetPropertyHistoryQuery query, CancellationToken cancellationToken)
  {
    var property = await context.LoadPropertyAsync(query.PropertyId, cancellationToken);
    var entries = await context.HistoryEntries.AsNoTracking().Where(h => h.PropertyId == property.Id).OrderByDescending(h => h.CreatedAt).ToListAsync(cancellationToken);
    var whyNot = property.IsActive ? await history.WhyNotAsync(property, null, cancellationToken) : $"{property.PropertyCode.Value} is off the register.";
    return Result<PropertyHistoryResult>.Success(new PropertyHistoryResult(await history.ToDtosAsync(entries, cancellationToken), whyNot is null, whyNot));
  }

  public async Task<Result<WaitingHistoriesResult>> Handle(GetWaitingHistoriesQuery query, CancellationToken cancellationToken)
  {
    var entries = await context.HistoryEntries.AsNoTracking().Where(h => h.Status == HistoryEntryStatus.Waiting).OrderBy(h => h.CreatedAt).ToListAsync(cancellationToken);
    return Result<WaitingHistoriesResult>.Success(new WaitingHistoriesResult(await history.ToDtosAsync(entries, cancellationToken)));
  }
}
