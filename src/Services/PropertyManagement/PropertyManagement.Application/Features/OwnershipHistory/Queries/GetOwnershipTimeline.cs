using Microsoft.EntityFrameworkCore;

/// Owner #Number in the order people first came to hold the property. IsCurrent: holds a share now.
public sealed record TimelineOwnerDto(
  int Number,
  Guid OwnerId,
  string OwnerCode,
  string OwnerName,
  string? FatherHusbandName,
  string? Cnic,
  string? Ntn,
  OldIdType? OldIdType,
  string? OldIdNo,
  bool IsDeceased,
  DateOnly? DateOfDeath,
  MasterRef? OwnerType,
  bool IsCurrent);

public sealed record TimelineShareDto(int OwnerNumber, Guid OwnerId, decimal SharePct);

/// One thing that happened to the ownership, oldest first. Kind: FirstOwners (owners entered with no transfer behind
/// them), Transfer (a registered transfer: From gave, To received), Allotment (an allottee confirmed as owner).
/// OwnershipIds: the holdings the event opened (a first holding is corrected through them). Documents: its papers.
public sealed record TimelineEventDto(
  string Kind,
  DateOnly Date,
  DatePrecision Precision,
  Guid? TransferId,
  string? TransferNo,
  MasterRef? TransferType,
  string? AllotmentNo,
  string? ReferenceNo,
  decimal? ConsiderationAmount,
  string? Relationship,
  string? ApprovalOrderRef,
  string? ApprovedBy,
  string? Remarks,
  IReadOnlyList<TimelineShareDto> From,
  IReadOnlyList<TimelineShareDto> To,
  IReadOnlyList<Guid> OwnershipIds,
  IReadOnlyList<DocumentDto> Documents);

public sealed record TimelineHoldingDto(int OwnerNumber, Guid OwnerId, Guid OwnershipId, decimal SharePct, DateOnly Since, DatePrecision SincePrecision, OwnershipStatus Status);

/// The property's ownership as one story: its original registration, every owner numbered, every event with its
/// papers, and who holds it now. CanEnterHistory: an ownership history entry fits (no earlier holders or transfers in
/// the register, the present holders entered with nothing behind them, none waiting). RestrictedDocuments: papers left
/// out because the caller may not see them (confidential / legal).
public sealed record OwnershipTimelineDto(
  PropertyRef Property,
  DateOnly? RegisteredOn,
  DatePrecision RegisteredOnPrecision,
  string? RegistrationReference,
  DateTime? EnteredOn,
  IReadOnlyList<TimelineOwnerDto> Owners,
  IReadOnlyList<TimelineEventDto> Events,
  IReadOnlyList<TimelineHoldingDto> Current,
  bool CanEnterHistory,
  Guid? WaitingHistoryId,
  int RestrictedDocuments);

public sealed record GetOwnershipTimelineResult(OwnershipTimelineDto Timeline);

public sealed record GetOwnershipTimelineQuery(Guid PropertyId) : IQuery<Result<GetOwnershipTimelineResult>>;

public class GetOwnershipTimelineHandler(IApplicationDbContext context, MasterLookup masters, DocumentService documents)
  : IQueryHandler<GetOwnershipTimelineQuery, Result<GetOwnershipTimelineResult>>
{
  private const string Remainder = "Remainder after ";

  public async Task<Result<GetOwnershipTimelineResult>> Handle(GetOwnershipTimelineQuery query, CancellationToken cancellationToken)
  {
    var propertyId = PropertyId.Of(query.PropertyId);
    var property = await context.Properties.AsNoTracking().FirstOrDefaultAsync(p => p.Id == propertyId, cancellationToken)
      ?? throw new PropertyNotFoundException($"Property {query.PropertyId} was not found.");

    var rows = await context.Ownerships.AsNoTracking().Where(o => o.PropertyId == propertyId).ToListAsync(cancellationToken);
    var transfers = await context.Transfers.AsNoTracking().Include(t => t.Parties)
      .Where(t => t.PropertyId == propertyId && t.TransferStatus == TransferStatus.Completed)
      .ToListAsync(cancellationToken);
    var allotmentIds = rows.Select(r => r.AcquiredViaAllotmentId).OfType<AllotmentId>().Distinct().ToList();
    var allotmentNos = await context.Allotments.AsNoTracking().Where(a => allotmentIds.Contains(a.Id))
      .ToDictionaryAsync(a => a.Id, a => a.AllotmentNo.Value, cancellationToken);
    var waiting = await context.HistoryEntries.AsNoTracking()
      .Where(h => h.PropertyId == propertyId && h.Status == HistoryEntryStatus.Waiting)
      .Select(h => (Guid?)h.Id.Value).FirstOrDefaultAsync(cancellationToken);

    // ---- the events, oldest first ----
    var events = new List<(DateOnly Date, int Order, DateTime? At, Func<Func<OwnerId, int>, IReadOnlyList<DocumentDto>, TimelineEventDto> Build, IReadOnlyList<OwnerId> Receivers, IReadOnlyList<Guid> Papers)>();

    // holdings with no transfer behind them: the first owners entered from the register (one event per start date)
    var entered = rows
      .Where(r => r.AcquiredViaTransferId is null && r.AcquiredViaAllotmentId is null && !(r.Remarks ?? "").StartsWith(Remainder, StringComparison.Ordinal))
      .GroupBy(r => r.EffectiveFrom)
      .ToList();
    foreach (var group in entered)
    {
      var held = group.ToList();
      var first = held[0];
      events.Add((first.EffectiveFrom, 0, first.CreatedAt,
        (number, papers) => new TimelineEventDto(
          "FirstOwners", first.EffectiveFrom, first.EffectiveFromPrecision, null, null, null, null,
          first.ReferenceNo, null, null, null, null, first.Remarks,
          [], held.Select(r => new TimelineShareDto(number(r.OwnerId), r.OwnerId.Value, r.OwnershipSharePct)).ToList(),
          held.Select(r => r.Id.Value).ToList(), papers),
        held.Select(r => r.OwnerId).ToList(), held.Select(r => r.Id.Value).ToList()));
    }

    // allottees confirmed as owners
    foreach (var group in rows.Where(r => r.AcquiredViaAllotmentId is not null).GroupBy(r => r.AcquiredViaAllotmentId!))
    {
      var held = group.ToList();
      var first = held[0];
      events.Add((first.EffectiveFrom, 1, first.CreatedAt,
        (number, papers) => new TimelineEventDto(
          "Allotment", first.EffectiveFrom, first.EffectiveFromPrecision, null, null, null, allotmentNos.GetValueOrDefault(group.Key),
          first.ReferenceNo, null, null, null, null, first.Remarks,
          [], held.Select(r => new TimelineShareDto(number(r.OwnerId), r.OwnerId.Value, r.OwnershipSharePct)).ToList(),
          held.Select(r => r.Id.Value).ToList(), papers),
        held.Select(r => r.OwnerId).ToList(), held.Select(r => r.Id.Value).ToList()));
    }

    // registered transfers
    foreach (var transfer in transfers)
    {
      var givers = transfer.Parties.Where(p => p.PartyRole == TransferPartyRole.Transferor).ToList();
      var receivers = transfer.Parties.Where(p => p.PartyRole == TransferPartyRole.Transferee).ToList();
      var opened = rows.Where(r => r.AcquiredViaTransferId == transfer.Id && r.EffectiveFrom == transfer.TransferDate).Select(r => r.Id.Value).ToList();
      events.Add((transfer.TransferDate, 2, transfer.CreatedAt,
        (number, papers) => new TimelineEventDto(
          "Transfer", transfer.TransferDate, transfer.TransferDatePrecision, transfer.Id.Value, transfer.TransferNo.Value, null, null,
          transfer.TransferReferenceNo, transfer.ConsiderationAmount, transfer.Relationship, transfer.ApprovalOrderRef, transfer.ApprovedBy, transfer.Remarks,
          givers.Select(p => new TimelineShareDto(number(p.OwnerId), p.OwnerId.Value, p.SharePct)).OrderBy(x => x.OwnerNumber).ToList(),
          receivers.Select(p => new TimelineShareDto(number(p.OwnerId), p.OwnerId.Value, p.SharePct)).OrderBy(x => x.OwnerNumber).ToList(),
          opened, papers),
        receivers.Select(p => p.OwnerId).ToList(), [transfer.Id.Value]));
    }

    var ordered = events.OrderBy(e => e.Date).ThenBy(e => e.Order).ThenBy(e => e.At).ToList();

    // ---- owners numbered in the order they first came to hold it (joint receivers of one event by name) ----
    var everyone = transfers.SelectMany(t => t.Parties).Select(p => p.OwnerId).Concat(rows.Select(r => r.OwnerId)).Distinct().ToList();
    var owners = await context.Owners.AsNoTracking().Where(o => everyone.Contains(o.Id)).ToListAsync(cancellationToken);
    var names = owners.ToDictionary(o => o.Id, o => o.OwnerName.Value);
    var numbers = new Dictionary<OwnerId, int>();
    foreach (var e in ordered)
      foreach (var ownerId in e.Receivers.OrderBy(id => names.GetValueOrDefault(id, ""), StringComparer.OrdinalIgnoreCase).ThenBy(id => id.Value))
        numbers.TryAdd(ownerId, numbers.Count + 1);
    foreach (var ownerId in everyone.OrderBy(id => names.GetValueOrDefault(id, ""), StringComparer.OrdinalIgnoreCase))
      numbers.TryAdd(ownerId, numbers.Count + 1);
    int Number(OwnerId id) => numbers.GetValueOrDefault(id);

    // ---- papers: of each transfer and each holding, latest version, those the caller may see ----
    var entityIds = ordered.SelectMany(e => e.Papers).ToList();
    var allPapers = await context.Documents.AsNoTracking()
      .Where(d => d.PropertyId == propertyId && d.IsActive && entityIds.Contains(d.EntityId)
        && (d.EntityType == DocumentEntityType.Transfer || d.EntityType == DocumentEntityType.Ownership))
      .OrderBy(d => d.UploadedAt).ToListAsync(cancellationToken);
    var paperIds = allPapers.Select(d => d.Id).ToList();
    var superseded = (await context.Documents.AsNoTracking()
        .Where(d => d.SupersedesDocumentId != null && paperIds.Contains(d.SupersedesDocumentId))
        .Select(d => d.SupersedesDocumentId!).ToListAsync(cancellationToken)).ToHashSet();
    var latest = allPapers.Where(d => !superseded.Contains(d.Id)).ToList();
    var visible = latest.Where(documents.MaySee).ToList();
    var restricted = latest.Count - visible.Count;

    var refs = await masters.Refs()
      .Add<TransferType>(transfers.Select(t => (MasterId?)t.TransferTypeId).Concat(rows.Select(r => r.AcquisitionTransferTypeId)))
      .Add<DocumentType>(visible.Select(d => (MasterId?)d.DocumentTypeId))
      .Add<OwnerType>(owners.Select(o => (MasterId?)o.OwnerTypeId))
      .LoadAsync(cancellationToken);

    var dtos = ordered.Select(e =>
    {
      var papers = visible.Where(d => e.Papers.Contains(d.EntityId)).Select(d => d.ToDto(refs, true)).ToList();
      var dto = e.Build(Number, papers);
      // the kind of transfer, or how a first owner acquired it
      var typeId = dto.TransferId is { } tid
        ? transfers.First(t => t.Id.Value == tid).TransferTypeId
        : rows.First(r => r.Id.Value == dto.OwnershipIds[0]).AcquisitionTransferTypeId;
      return dto with { TransferType = typeId is null ? null : refs[typeId] };
    }).ToList();

    var current = rows.Where(r => r.IsCurrent).ToList();
    var currentOwners = current.Select(r => r.OwnerId).ToHashSet();
    var ownerDtos = owners
      .Select(o => new TimelineOwnerDto(
        Number(o.Id), o.Id.Value, o.OwnerCode.Value, o.OwnerName.Value, o.FatherHusbandName, o.Cnic?.Value, o.Ntn,
        o.OldIdType, o.OldIdNo, o.IsDeceased, o.DateOfDeath, refs[o.OwnerTypeId], currentOwners.Contains(o.Id)))
      .OrderBy(o => o.Number).ToList();

    var holdings = current
      .Select(r => new TimelineHoldingDto(Number(r.OwnerId), r.OwnerId.Value, r.Id.Value, r.OwnershipSharePct,
        OwnershipHistory.HoldingSince(r, rows), r.EffectiveFromPrecision, r.OwnershipStatus))
      .OrderByDescending(h => h.SharePct).ThenBy(h => h.OwnerNumber).ToList();

    var canEnter = waiting is null && property.IsActive
      && rows.All(r => r.IsCurrent && r.AcquiredViaTransferId is null && r.AcquiredViaAllotmentId is null);

    return Result<GetOwnershipTimelineResult>.Success(new GetOwnershipTimelineResult(new OwnershipTimelineDto(
      new PropertyRef(property.Id.Value, property.PropertyCode.Value, property.PropertyName.Value),
      property.RegisteredOn, property.RegisteredOnPrecision, property.RegistrationReference, property.CreatedAt,
      ownerDtos, dtos, holdings, canEnter, waiting, restricted)));
  }
}
