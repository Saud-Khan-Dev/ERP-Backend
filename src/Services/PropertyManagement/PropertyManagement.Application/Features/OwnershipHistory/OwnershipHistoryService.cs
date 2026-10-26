using FluentValidation;
using Microsoft.EntityFrameworkCore;

/// One owner's share in a step: given (a giver) or received (a receiver, or a first owner).
public sealed record HistoryShareInput(Guid OwnerId, decimal SharePct);

/// One step of an ownership history, oldest first. Step 1 (FirstOwners): Receivers are the first owners on record with
/// their shares (100% together); TransferTypeId may say how they acquired it (First Registration, Allotment ...).
/// Every later step (Transfer): Givers pass shares to Receivers (the two sides balance); TransferTypeId required, and
/// Relationship for a gift or inheritance. Date: the day, or the first day of the month / year when only that is known
/// (Precision Month / Year), or roughly (About). ReferenceNo: the deed / mutation / registry no. DocumentIds: the step's
/// papers, filed with POST /ownership-history/{id}/steps/{step}/documents (empty when the entry is first entered).
public sealed record HistoryStepInput(
  HistoryStepKind Kind,
  DateOnly Date,
  DatePrecision Precision = DatePrecision.Exact,
  Guid? TransferTypeId = null,
  string? ReferenceNo = null,
  decimal? ConsiderationAmount = null,
  string? Relationship = null,
  string? ApprovalOrderRef = null,
  string? Remarks = null,
  IReadOnlyList<HistoryShareInput>? Givers = null,
  IReadOnlyList<HistoryShareInput>? Receivers = null,
  IReadOnlyList<Guid>? DocumentIds = null)
{
  public HistoryStep ToStep() => new(
    Kind, Date, Precision, TransferTypeId, ReferenceNo, ConsiderationAmount, Relationship, ApprovalOrderRef, Remarks,
    (Givers ?? Array.Empty<HistoryShareInput>()).Select(g => new HistoryShare(g.OwnerId, g.SharePct)).ToList(),
    (Receivers ?? Array.Empty<HistoryShareInput>()).Select(r => new HistoryShare(r.OwnerId, r.SharePct)).ToList(),
    DocumentIds ?? Array.Empty<Guid>());
}

public class HistoryStepInputValidator : AbstractValidator<HistoryStepInput>
{
  public HistoryStepInputValidator()
  {
    RuleFor(x => x.Kind).IsInEnum();
    RuleFor(x => x.Precision).IsInEnum();
    RuleFor(x => x.Date).NotEmpty().WithMessage("Give the date of the step (or its month or year).");
    RuleFor(x => x.ReferenceNo).MaximumLength(100);
    RuleFor(x => x.Relationship).MaximumLength(100);
    RuleFor(x => x.ApprovalOrderRef).MaximumLength(100);
    RuleFor(x => x.Remarks).MaximumLength(4000);
    RuleFor(x => x.ConsiderationAmount).GreaterThanOrEqualTo(0).When(x => x.ConsiderationAmount is not null);
    RuleForEach(x => x.Givers).ChildRules(share =>
    {
      share.RuleFor(s => s.OwnerId).NotEmpty().WithMessage("Choose who gave the share.");
      share.RuleFor(s => s.SharePct).GreaterThan(0).LessThanOrEqualTo(100).WithMessage("A share is more than 0% and at most 100%.");
    });
    RuleForEach(x => x.Receivers).ChildRules(share =>
    {
      share.RuleFor(s => s.OwnerId).NotEmpty().WithMessage("Choose who received the share.");
      share.RuleFor(s => s.SharePct).GreaterThan(0).LessThanOrEqualTo(100).WithMessage("A share is more than 0% and at most 100%.");
    });
  }
}

public sealed record HistoryShareDto(OwnerRef? Owner, decimal SharePct);

/// DateText: the date as it is known ("14 Mar 1985", "March 1972", "1972", "about 1972"). Registered*: what the step
/// became once the entry was registered (the transfer; the first step's holdings).
public sealed record HistoryStepDto(
  int StepNo,
  HistoryStepKind Kind,
  DateOnly Date,
  DatePrecision Precision,
  MasterRef? TransferType,
  string? ReferenceNo,
  decimal? ConsiderationAmount,
  string? Relationship,
  string? ApprovalOrderRef,
  string? Remarks,
  IReadOnlyList<HistoryShareDto> Givers,
  IReadOnlyList<HistoryShareDto> Receivers,
  IReadOnlyList<Guid> DocumentIds,
  Guid? RegisteredTransferId,
  string? RegisteredTransferNo);

/// RecordedByUserId: who entered it (the PROPERTY_TRANSFER rule keeps them from registering it themselves).
public sealed record HistoryEntryDto(
  Guid Id,
  string EntryNo,
  PropertyRef? Property,
  HistoryEntryStatus Status,
  IReadOnlyList<HistoryStepDto> Steps,
  string? Remarks,
  string RecordedBy,
  DateOnly RecordedOn,
  Guid? RecordedByUserId,
  string? DecidedBy,
  DateOnly? DecidedOn,
  string? DecisionReason,
  DateTime? CreatedAt);

/// Loads what an ownership history is played against, and reads entries back as DTOs.
public class OwnershipHistoryService(IApplicationDbContext context, MasterLookup masters, PropertyReadService read)
{
  public async Task<OwnershipHistoryEntry> LoadAsync(Guid id, CancellationToken cancellationToken) =>
      await context.HistoryEntries.FirstOrDefaultAsync(h => h.Id == HistoryEntryId.Of(id), cancellationToken)
      ?? throw new HistoryEntryNotFoundException($"Ownership history {id} was not found.");

  /// The property's ownership rows (tracked: registering adopts the present ones), every owner the steps name plus the
  /// present holders, the transfer types the steps use, the tenure new holders get (Owned), the allotment in force.
  public async Task<HistoryContext> ContextAsync(Property property, IReadOnlyList<HistoryStep> steps, CancellationToken cancellationToken)
  {
    var rows = await context.Ownerships.Where(o => o.PropertyId == property.Id).ToListAsync(cancellationToken);

    var named = steps.SelectMany(s => s.Givers.Concat(s.Receivers)).Select(s => s.OwnerId).Where(id => id != Guid.Empty).Select(OwnerId.Of);
    var wanted = named.Concat(rows.Select(r => r.OwnerId)).Distinct().ToList();
    var owners = await context.Owners.AsNoTracking().Where(o => wanted.Contains(o.Id)).ToDictionaryAsync(o => o.Id, cancellationToken);

    var types = new Dictionary<MasterId, TransferType>();
    foreach (var typeId in steps.Select(s => s.TransferTypeId).OfType<Guid>().Distinct())
      types[MasterId.Of(typeId)] = await masters.GetAsync<TransferType>(typeId, cancellationToken);

    var tenure = await TransferRegistration.NewcomerTenureAsync(masters, null, cancellationToken);
    var allotment = await context.AllotmentInForceAsync(property.Id, except: null, cancellationToken);
    return new HistoryContext(property, rows, owners, types, tenure, allotment, PropertyWriteGuard.Today());
  }

  /// Papers named on the steps must be this entry's own (filed with it, still in force).
  public async Task EnsureOwnPapersAsync(OwnershipHistoryEntry entry, IReadOnlyList<HistoryStep> steps, CancellationToken cancellationToken)
  {
    var ids = steps.SelectMany(s => s.DocumentIds).Distinct().Select(DocumentId.Of).ToList();
    if (ids.Count == 0)
      return;

    var own = await context.Documents.AsNoTracking()
      .Where(d => ids.Contains(d.Id) && d.EntityType == DocumentEntityType.OwnershipHistory && d.EntityId == entry.Id.Value && d.IsActive)
      .CountAsync(cancellationToken);
    if (own != ids.Count)
      throw new DomainException($"A paper listed on a step was not filed with {entry.EntryNo.Value} (or was taken off). File it on the step itself.");
  }

  public async Task<List<HistoryEntryDto>> ToDtosAsync(IReadOnlyCollection<OwnershipHistoryEntry> entries, CancellationToken cancellationToken)
  {
    var steps = entries.SelectMany(e => e.Steps).ToList();
    var refs = await masters.Refs().Add<TransferType>(steps.Select(s => s.TransferTypeId is { } id ? MasterId.Of(id) : null)).LoadAsync(cancellationToken);
    var owners = await read.OwnerRefsAsync(steps.SelectMany(s => s.Givers.Concat(s.Receivers)).Select(s => (OwnerId?)OwnerId.Of(s.OwnerId)), cancellationToken);
    var properties = await read.PropertyRefsAsync(entries.Select(e => e.PropertyId), cancellationToken);

    var transferIds = steps.Select(s => s.RegisteredTransferId).OfType<Guid>().Select(TransferId.Of).ToList();
    var transferNos = transferIds.Count == 0
      ? new Dictionary<Guid, string>()
      : await context.Transfers.AsNoTracking().Where(t => transferIds.Contains(t.Id))
        .ToDictionaryAsync(t => t.Id.Value, t => t.TransferNo.Value, cancellationToken);

    HistoryShareDto Share(HistoryShare s) => new(owners.GetValueOrDefault(OwnerId.Of(s.OwnerId)), s.SharePct);

    return entries.Select(e => new HistoryEntryDto(
      e.Id.Value, e.EntryNo.Value, properties.GetValueOrDefault(e.PropertyId), e.Status,
      e.Steps.Select((s, i) => new HistoryStepDto(
        i + 1, s.Kind, s.Date, s.Precision, s.TransferTypeId is { } typeId ? refs[MasterId.Of(typeId)] : null,
        s.ReferenceNo, s.ConsiderationAmount, s.Relationship, s.ApprovalOrderRef, s.Remarks,
        s.Givers.Select(Share).ToList(), s.Receivers.Select(Share).ToList(), s.DocumentIds,
        s.RegisteredTransferId, s.RegisteredTransferId is { } t ? transferNos.GetValueOrDefault(t) : null)).ToList(),
      e.Remarks, e.RecordedBy, e.RecordedOn, e.CreatedBy, e.DecidedBy, e.DecidedOn, e.DecisionReason, e.CreatedAt)).ToList();
  }
}
