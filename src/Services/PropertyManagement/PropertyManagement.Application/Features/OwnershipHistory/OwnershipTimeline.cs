using FluentValidation;
using Microsoft.EntityFrameworkCore;

public enum TimelineEventKind { FirstOwners, Transfer, AllotteeConfirmed, ShareEnded }

/// An owner as the timeline numbers them: #1, #2 … in the order they first held a share; Current: holds one now.
public sealed record TimelineOwnerDto(int No, OwnerRef Owner, bool Current, decimal SharePct, DateOnly? DateOfDeath);
public sealed record TimelineShareDto(int OwnerNo, decimal SharePct);
public sealed record TimelinePaperDto(Guid Id, string Title, string FileName);
/// One event, oldest first. FromHistory: a transfer entered from the old records (it can be corrected); CanCorrect: the
/// first owners can still be corrected (no transfer came after them).
public sealed record TimelineEventDto(
  TimelineEventKind Kind, DateOnly Date, DatePrecision Precision, Guid? TransferId, string? TransferNo, MasterRef? TransferType, string? ReferenceNo,
  string? Relationship, bool FromHistory, bool CanCorrect, IReadOnlyList<TimelineShareDto> Givers, IReadOnlyList<TimelineShareDto> Receivers,
  IReadOnlyList<TimelinePaperDto> Papers);
public sealed record OwnershipTimelineResult(IReadOnlyList<TimelineEventDto> Events, IReadOnlyList<TimelineOwnerDto> Owners);

public sealed record GetOwnershipTimelineQuery(Guid PropertyId) : IQuery<Result<OwnershipTimelineResult>>;

/// Corrects the first owners while no transfer came after them: who and what share (together 100%), with the reason.
public sealed record CorrectFirstOwnersCommand(Guid PropertyId, IReadOnlyList<OwnershipInput> Owners, string Reason) : ICommand<Result<HistoryEntryResult>>;
/// Corrects a transfer entered from the old records: its date (and how well it is known), kind, deed and relationship.
public sealed record CorrectHistoryTransferCommand(Guid TransferId, Guid TransferTypeId, DateOnly TransferDate, DatePrecision Precision,
  string? ReferenceNo, string? Relationship, string Reason) : ICommand<Result<HistoryEntryResult>>;

public class CorrectFirstOwnersCommandValidator : AbstractValidator<CorrectFirstOwnersCommand>
{
  public CorrectFirstOwnersCommandValidator()
  {
    RuleFor(x => x.Owners).NotEmpty();
    RuleForEach(x => x.Owners).SetValidator(new OwnershipInputValidator());
    RuleFor(x => x.Reason).NotEmpty().WithMessage("Say why it is corrected (e.g. the mutation register names Fazal, not Faisal).").MaximumLength(1000);
  }
}

public class CorrectHistoryTransferCommandValidator : AbstractValidator<CorrectHistoryTransferCommand>
{
  public CorrectHistoryTransferCommandValidator()
  {
    RuleFor(x => x.TransferTypeId).NotEmpty();
    RuleFor(x => x.Precision).IsInEnum();
    RuleFor(x => x.ReferenceNo).MaximumLength(100);
    RuleFor(x => x.Relationship).MaximumLength(100);
    RuleFor(x => x.Reason).NotEmpty().WithMessage("Say why it is corrected (e.g. the mutation register shows 1974, not 1975).").MaximumLength(1000);
  }
}

public class OwnershipTimelineHandlers(
    IApplicationDbContext context, MasterLookup masters, DocumentService documents, ICurrentUser currentUser, IDutyRules dutyRules, IAuditTrail audit)
  : IQueryHandler<GetOwnershipTimelineQuery, Result<OwnershipTimelineResult>>,
    ICommandHandler<CorrectFirstOwnersCommand, Result<HistoryEntryResult>>,
    ICommandHandler<CorrectHistoryTransferCommand, Result<HistoryEntryResult>>
{
  public async Task<Result<OwnershipTimelineResult>> Handle(GetOwnershipTimelineQuery query, CancellationToken cancellationToken)
  {
    var property = await context.LoadPropertyAsync(query.PropertyId, cancellationToken);
    var rows = await context.Ownerships.AsNoTracking().Where(o => o.PropertyId == property.Id).ToListAsync(cancellationToken);
    var transfers = await context.Transfers.AsNoTracking().Include(t => t.Parties)
      .Where(t => t.PropertyId == property.Id && t.TransferStatus == TransferStatus.Completed).ToListAsync(cancellationToken);
    var ownerIds = rows.Select(r => r.OwnerId).Concat(transfers.SelectMany(t => t.Parties).Select(p => p.OwnerId)).Distinct().ToList();
    var owners = await context.Owners.AsNoTracking().Where(o => ownerIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, cancellationToken);
    var transferIds = transfers.Select(t => t.Id.Value).ToList();
    var papers = (await context.Documents.AsNoTracking()
        .Where(d => d.IsActive && d.EntityType == DocumentEntityType.Transfer && transferIds.Contains(d.EntityId)).ToListAsync(cancellationToken))
      .Where(documents.MaySee).ToLookup(d => d.EntityId);
    var refs = await masters.Refs().Add<TransferType>(transfers.Select(t => (MasterId?)t.TransferTypeId).Concat(rows.Select(r => r.AcquisitionTransferTypeId))).LoadAsync(cancellationToken);

    // owners numbered #1, #2 … in the order they first held a share
    var numbers = new Dictionary<OwnerId, int>();
    foreach (var row in rows.OrderBy(r => r.EffectiveFrom).ThenBy(r => r.OpenedByTransferId is null ? 0 : 1))
      numbers.TryAdd(row.OwnerId, numbers.Count + 1);
    foreach (var id in ownerIds)
      numbers.TryAdd(id, numbers.Count + 1);
    TimelineShareDto Share(OwnerId owner, decimal pct) => new(numbers[owner], pct);

    var events = new List<(DateOnly Date, int Order, TimelineEventDto Dto)>();
    var firstRows = rows.Where(r => r.OpenedByTransferId is null && r.AcquiredViaTransferId is null && r.AcquiredViaAllotmentId is null).ToList();
    foreach (var group in firstRows.GroupBy(r => r.EffectiveFrom))
    {
      var any = group.First();
      events.Add((group.Key, 0, new TimelineEventDto(TimelineEventKind.FirstOwners, group.Key, any.EffectiveFromPrecision, null, null,
        refs[any.AcquisitionTransferTypeId], any.ReferenceNo, null, false, transfers.Count == 0, [],
        group.Select(r => Share(r.OwnerId, r.OwnershipSharePct)).ToList(), [])));
    }
    foreach (var group in rows.Where(r => r.AcquiredViaAllotmentId is not null).GroupBy(r => r.EffectiveFrom))
      events.Add((group.Key, 1, new TimelineEventDto(TimelineEventKind.AllotteeConfirmed, group.Key, DatePrecision.Exact, null, null, null, group.First().ReferenceNo,
        null, false, false, [], group.Select(r => Share(r.OwnerId, r.OwnershipSharePct)).ToList(), [])));
    foreach (var t in transfers)
      events.Add((t.TransferDate, 2, new TimelineEventDto(TimelineEventKind.Transfer, t.TransferDate, t.TransferDatePrecision, t.Id.Value, t.TransferNo.Value,
        refs[t.TransferTypeId], t.TransferReferenceNo, t.Relationship, t.FromHistoryEntryId is not null, false,
        t.Parties.Where(p => p.PartyRole == TransferPartyRole.Transferor).Select(p => Share(p.OwnerId, p.SharePct)).ToList(),
        t.Parties.Where(p => p.PartyRole == TransferPartyRole.Transferee).Select(p => Share(p.OwnerId, p.SharePct)).ToList(),
        papers[t.Id.Value].Select(d => new TimelinePaperDto(d.Id.Value, d.Title ?? d.OriginalFileName, d.OriginalFileName)).ToList())));
    // a share that ended without a transfer (an allotment cancelled, an entry closed before transfers ruled)
    foreach (var group in rows.Where(r => r.EffectiveTo is not null && r.ClosedByTransferId is null && !transfers.Any(t => t.TransferDate == r.EffectiveTo && t.Parties.Any(p => p.OwnerId == r.OwnerId)))
        .GroupBy(r => r.EffectiveTo!.Value))
      events.Add((group.Key, 3, new TimelineEventDto(TimelineEventKind.ShareEnded, group.Key, DatePrecision.Exact, null, null, null, group.First().EndReferenceNo, null,
        false, false, group.Select(r => Share(r.OwnerId, r.OwnershipSharePct)).ToList(), [], [])));
    var dtos = events.OrderBy(e => e.Date).ThenBy(e => e.Order).Select(e => e.Dto).ToList();

    var current = rows.Where(r => r.IsCurrent).GroupBy(r => r.OwnerId).ToDictionary(g => g.Key, g => g.Sum(r => r.OwnershipSharePct));
    var ownerDtos = numbers.OrderBy(n => n.Value).Where(n => owners.ContainsKey(n.Key))
      .Select(n => new TimelineOwnerDto(n.Value, owners[n.Key].ToRef(), current.ContainsKey(n.Key), current.GetValueOrDefault(n.Key), owners[n.Key].DateOfDeath)).ToList();
    return Result<OwnershipTimelineResult>.Success(new OwnershipTimelineResult(dtos, ownerDtos));
  }

  public async Task<Result<HistoryEntryResult>> Handle(CorrectFirstOwnersCommand command, CancellationToken cancellationToken)
  {
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);
    var rows = await context.Ownerships.Where(o => o.PropertyId == property.Id).ToListAsync(cancellationToken);
    if (rows.Count == 0)
      throw new DomainException($"{property.PropertyCode.Value} has no owners on the register to correct.");
    if (rows.Any(r => r.OpenedByTransferId is not null || r.ClosedByTransferId is not null || r.AcquiredViaTransferId is not null || r.AcquiredViaAllotmentId is not null || !r.IsCurrent)
        || await context.Transfers.AnyAsync(t => t.PropertyId == property.Id && t.TransferStatus != TransferStatus.Cancelled && t.TransferStatus != TransferStatus.Rejected, cancellationToken))
      throw new DomainException("A transfer was recorded after the first owners. So they can be corrected only by another transfer.");
    var rowIds = rows.Select(r => r.Id).ToList();
    if (await context.Encumbrances.AnyAsync(e => e.PropertyId == property.Id && e.OwnershipId != null && rowIds.Contains(e.OwnershipId!), cancellationToken))
      throw new DomainException("A charge is registered against one of these shares. Release the charge before correcting the owners.");

    // the PROPERTY_TRANSFER rule: who entered the owners does not correct them
    foreach (var maker in rows.Select(r => r.CreatedBy).Distinct())
      if (await dutyRules.SeparationRefusalAsync(DutyRules.PropertyTransfer, maker, currentUser.UserId, $"correct the first owners of {property.PropertyCode.Value}, which you entered", cancellationToken) is { } refusal)
        throw new DomainException(refusal);

    var first = rows[0];
    var owners = new List<PropertyOwnership.FirstOwner>();
    foreach (var input in command.Owners)
      owners.Add(new PropertyOwnership.FirstOwner(await context.LoadOwnerAsync(input.OwnerId, cancellationToken), await masters.GetAsync<TenureType>(input.TenureTypeId, cancellationToken),
        input.OwnershipSharePct, first.EffectiveFrom, await masters.GetOptionalAsync<TransferType>(input.AcquisitionTransferTypeId, cancellationToken),
        input.ReferenceNo ?? first.ReferenceNo, input.Remarks, first.EffectiveFromPrecision));

    // the corrected owners replace the entry; the audit trail keeps what stood before, with the reason
    context.Ownerships.RemoveRange(rows);
    var record = new OwnershipRecord(property, [], owners.Select(o => o.Owner), [], [], PakistanDate.Today);
    var corrected = PropertyOwnership.RegisterFirstOwners(owners, record);
    await context.Ownerships.AddRangeAsync(corrected, cancellationToken);
    audit.Record(PropertyAuditActions.Corrected, property, $"First owners corrected: {command.Reason}");
    await context.SaveChangesAsync(cancellationToken);
    return Result<HistoryEntryResult>.Success(new HistoryEntryResult(property.Id.Value, property.PropertyCode.Value, HistoryEntryStatus.Registered, []));
  }

  public async Task<Result<HistoryEntryResult>> Handle(CorrectHistoryTransferCommand command, CancellationToken cancellationToken)
  {
    var transfer = await context.LoadTransferAsync(command.TransferId, cancellationToken);
    if (transfer.FromHistoryEntryId is not { } entryId)
      throw new DomainException($"{transfer.TransferNo.Value} was not entered from the old records. To correct a transfer, record another transfer.");
    var entry = await context.HistoryEntries.AsNoTracking().FirstAsync(h => h.Id == entryId, cancellationToken);
    foreach (var (maker, act) in new[] { (entry.CreatedBy, "entered"), (entry.LastEditedByUserId, "changed") })
      if (await dutyRules.SeparationRefusalAsync(DutyRules.PropertyTransfer, maker, currentUser.UserId, $"correct {transfer.TransferNo.Value}, whose history you {act}", cancellationToken) is { } refusal)
        throw new DomainException(refusal);

    var property = await context.LoadPropertyAsync(transfer.PropertyId.Value, cancellationToken);
    var record = await context.LoadOwnershipRecordAsync(property, transfer.Parties.Select(p => p.OwnerId), cancellationToken);
    var date = command.Precision switch
    {
      DatePrecision.Month => new DateOnly(command.TransferDate.Year, command.TransferDate.Month, 1),
      DatePrecision.Year or DatePrecision.About => new DateOnly(command.TransferDate.Year, 1, 1),
      _ => command.TransferDate,
    };
    // it stays between the step before it and the step after it
    var others = await context.Transfers.AsNoTracking()
      .Where(t => t.PropertyId == property.Id && t.Id != transfer.Id && t.TransferStatus == TransferStatus.Completed).Select(t => t.TransferDate).ToListAsync(cancellationToken);
    var starts = record.Ownerships.Where(r => r.OpenedByTransferId is null).Select(r => r.EffectiveFrom);
    var before = others.Concat(starts).Where(d => d < transfer.TransferDate).DefaultIfEmpty().Max();
    var after = others.Where(d => d > transfer.TransferDate).DefaultIfEmpty().Min();
    transfer.CorrectHistory(await masters.GetAsync<TransferType>(command.TransferTypeId, cancellationToken), date, command.Precision, command.ReferenceNo,
      command.Relationship, record, before == default ? null : before, after == default ? null : after);
    audit.Record(PropertyAuditActions.Corrected, transfer, command.Reason);
    await context.SaveChangesAsync(cancellationToken);
    return Result<HistoryEntryResult>.Success(new HistoryEntryResult(transfer.Id.Value, transfer.TransferNo.Value, HistoryEntryStatus.Registered, [transfer.TransferNo.Value]));
  }
}
