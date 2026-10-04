using Microsoft.EntityFrameworkCore;

public class GetTransfersHandler(IApplicationDbContext context, MasterLookup masters, PropertyReadService read)
  : IQueryHandler<GetTransfersQuery, Result<GetTransfersQueryResult>>
{
  public async Task<Result<GetTransfersQueryResult>> Handle(GetTransfersQuery query, CancellationToken cancellationToken)
  {
    var scope = RegisterScope.Create(context, query.PropertyId, query.TownId, query.Search);
    var transfers = context.Transfers.AsNoTracking();

    if (scope.Properties is { } allowed)
      transfers = transfers.Where(t => allowed.Contains(t.PropertyId));

    if (query.From is { } from)
      transfers = transfers.Where(t => t.TransferDate >= from);

    if (query.To is { } to)
      transfers = transfers.Where(t => t.TransferDate <= to);

    if (query.Status is { } status)
      transfers = transfers.Where(t => t.TransferStatus == status);

    if (query.TransferTypeId is { } typeId)
    {
      var type = MasterId.Of(typeId);
      transfers = transfers.Where(t => t.TransferTypeId == type);
    }

    if (scope.HasSearch)
    {
      var (upper, lower, matchingProperties, matchingOwners) = (scope.Upper!, scope.Lower!, scope.SearchProperties, scope.SearchOwners);
      transfers = transfers.Where(t => ((string)(object)t.TransferNo).Contains(upper)
        || (t.TransferReferenceNo != null && t.TransferReferenceNo.ToLower().Contains(lower))
        || t.Parties.Any(p => matchingOwners.Contains(p.OwnerId))
        || matchingProperties.Contains(t.PropertyId));
    }

    var total = await transfers.LongCountAsync(cancellationToken);

    var propertyCodes = context.Properties.AsNoTracking();
    var desc = query.SortDescending;
    var ordered = query.SortBy switch
    {
      TransferSort.Code => desc ? transfers.OrderByDescending(t => t.TransferNo) : transfers.OrderBy(t => t.TransferNo),
      TransferSort.Property => desc
        ? transfers.OrderByDescending(t => propertyCodes.Where(p => p.Id == t.PropertyId).Select(p => p.PropertyCode).FirstOrDefault())
        : transfers.OrderBy(t => propertyCodes.Where(p => p.Id == t.PropertyId).Select(p => p.PropertyCode).FirstOrDefault()),
      _ => desc ? transfers.OrderByDescending(t => t.TransferDate) : transfers.OrderBy(t => t.TransferDate),
    };

    var page = await ordered.ThenByDescending(t => t.TransferNo)
        .Skip(query.Pagination.Pageindex * query.Pagination.PageSize)
        .Take(query.Pagination.PageSize)
        .Include(t => t.Parties)
        .ToListAsync(cancellationToken);

    var headers = await read.PropertyHeadersAsync(page.Select(t => t.PropertyId), cancellationToken);
    var owners = await read.OwnerRefsAsync(page.SelectMany(t => t.Parties).Select(p => p.OwnerId), cancellationToken);
    var refs = await masters.Refs().Add<TransferType>(page.Select(t => t.TransferTypeId)).LoadAsync(cancellationToken);

    List<TransferPartyRowDto> Side(PropertyTransfer transfer, TransferPartyRole role) => transfer.Parties
        .Where(p => p.PartyRole == role)
        .OrderByDescending(p => p.SharePct)
        .Select(p => owners.GetValueOrDefault(p.OwnerId) is { } owner
          ? new TransferPartyRowDto(owner.Id, owner.OwnerCode, owner.OwnerName, p.SharePct)
          : new TransferPartyRowDto(p.OwnerId.Value, string.Empty, string.Empty, p.SharePct))
        .ToList();

    var data = page.Select(t =>
    {
      var property = headers[t.PropertyId];
      return new TransferListItemDto(
        t.Id.Value, t.TransferNo.Value, property.Id, property.PropertyCode, property.PropertyName, property.Town,
        refs[t.TransferTypeId], t.TransferDate, t.TransferStatus, t.ConsiderationAmount, t.ShareTransferredPct, t.TransferReferenceNo,
        Side(t, TransferPartyRole.Transferor), Side(t, TransferPartyRole.Transferee));
    }).ToList();

    return Result<GetTransfersQueryResult>.Success(new GetTransfersQueryResult(
      new PaginatedResult<TransferListItemDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }
}
