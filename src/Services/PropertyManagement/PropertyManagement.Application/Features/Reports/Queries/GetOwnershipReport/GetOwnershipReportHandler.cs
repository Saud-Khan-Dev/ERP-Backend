using Microsoft.EntityFrameworkCore;

/// A share counts on the report date when its period covers that date — effective_from <= asOf and
/// (effective_to is null or effective_to > asOf; the day a share ends it already belongs to the next holder,
/// which is how a completed transfer closes and opens rows) — and its status is:
///   ACTIVE   — a current share;
///   ENDED    — a share that has ended since, but was held on that date (history read as of then);
///   DISPUTED — left out unless includeDisputed=true: the domain stops counting a disputed share as ownership
///              (it is not current and not in the 100% total), and the date the dispute began is not recorded.
/// Ownership has no cancelled status: a share entered in error is ended on its first day, so its period is
/// empty and it never shows.
public class GetOwnershipReportHandler(IApplicationDbContext context, MasterLookup masters, PropertyReadService read)
  : IQueryHandler<GetOwnershipReportQuery, Result<GetOwnershipReportQueryResult>>
{
  public const int MaxRows = 5000;

  public async Task<Result<GetOwnershipReportQueryResult>> Handle(GetOwnershipReportQuery query, CancellationToken cancellationToken)
  {
    var asOf = query.AsOf ?? DateOnly.FromDateTime(DateTime.UtcNow);

    var ownerships = context.Ownerships.AsNoTracking()
        .Where(o => o.EffectiveFrom <= asOf && (o.EffectiveTo == null || o.EffectiveTo > asOf));

    ownerships = query.IncludeDisputed
      ? ownerships.Where(o => o.OwnershipStatus == OwnershipStatus.Active || o.OwnershipStatus == OwnershipStatus.Ended || o.OwnershipStatus == OwnershipStatus.Disputed)
      : ownerships.Where(o => o.OwnershipStatus == OwnershipStatus.Active || o.OwnershipStatus == OwnershipStatus.Ended);

    var properties = context.Properties.AsNoTracking();
    if (!query.IncludeInactive)
      properties = properties.Where(p => p.IsActive);
    if (query.TownId is { } townId)
    {
      var town = MasterId.Of(townId);
      properties = properties.Where(p => p.TownId == town);
    }
    if (query.PropertyId is { } propertyGuid)
    {
      var propertyId = PropertyId.Of(propertyGuid);
      properties = properties.Where(p => p.Id == propertyId);
    }

    var propertyIds = properties.Select(p => p.Id);
    ownerships = ownerships.Where(o => propertyIds.Contains(o.PropertyId));

    if (query.OwnerId is { } ownerGuid)
    {
      var ownerId = OwnerId.Of(ownerGuid);
      ownerships = ownerships.Where(o => o.OwnerId == ownerId);
    }

    if (query.OwnerTypeId is { } ownerTypeId)
    {
      var type = MasterId.Of(ownerTypeId);
      var ofType = context.Owners.Where(o => o.OwnerTypeId == type).Select(o => o.Id);
      ownerships = ownerships.Where(o => ofType.Contains(o.OwnerId));
    }

    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var term = query.Search.Trim();
      var upper = term.ToUpperInvariant();
      var lower = term.ToLowerInvariant();

      // CNICs are stored as 12345-1234567-1: a full CNIC typed without dashes still matches
      var cnic = term;
      try { cnic = Cnic.Of(term).Value; } catch (DomainException) { }

      var matchingProperties = context.Properties
          .Where(p => ((string)(object)p.PropertyCode).Contains(upper) || p.PropertyName.Value.ToLower().Contains(lower))
          .Select(p => p.Id);
      var matchingOwners = context.Owners
          .Where(o => ((string)(object)o.OwnerCode).Contains(upper) || o.OwnerName.Value.ToLower().Contains(lower)
            || (o.Cnic != null && ((string)(object)o.Cnic).Contains(cnic)))
          .Select(o => o.Id);

      ownerships = ownerships.Where(o => matchingProperties.Contains(o.PropertyId) || matchingOwners.Contains(o.OwnerId)
        || (o.ReferenceNo != null && o.ReferenceNo.ToLower().Contains(lower)));
    }

    var count = await ownerships.CountAsync(cancellationToken);
    if (count > MaxRows)
      return Result<GetOwnershipReportQueryResult>.Failure($"The report would list {count} ownership rows. Narrow the filters to {MaxRows} or fewer.");

    var rows = await ownerships.ToListAsync(cancellationToken);

    var headers = await read.PropertyHeadersAsync(rows.Select(o => o.PropertyId), cancellationToken);
    var owners = await read.OwnersAsync(rows.Select(o => o.OwnerId), cancellationToken);
    var refs = await masters.Refs()
        .Add<TenureType>(rows.Select(o => o.TenureTypeId))
        .Add<TransferType>(rows.Select(o => o.AcquisitionTransferTypeId))
        .Add<OwnerType>(owners.Values.Select(o => o.OwnerTypeId))
        .LoadAsync(cancellationToken);

    var data = rows.Select(o =>
    {
      var property = headers[o.PropertyId];
      var owner = owners[o.OwnerId];
      return new OwnershipReportRowDto(
        o.Id.Value, property.Id, property.PropertyCode, property.PropertyName, property.Town,
        owner.Id.Value, owner.OwnerCode.Value, owner.OwnerName.Value, refs[owner.OwnerTypeId], owner.Cnic?.Value,
        o.OwnershipSharePct, refs[o.TenureTypeId], o.EffectiveFrom, o.EffectiveTo,
        o.AcquisitionTransferTypeId is null ? null : refs[o.AcquisitionTransferTypeId], o.ReferenceNo, o.OwnershipStatus);
    })
    .OrderBy(r => r.PropertyCode, StringComparer.Ordinal)
    .ThenByDescending(r => r.SharePct)
    .ThenBy(r => r.OwnerCode, StringComparer.Ordinal)
    .ToList();

    return Result<GetOwnershipReportQueryResult>.Success(new GetOwnershipReportQueryResult(asOf, data.Count, data));
  }
}
