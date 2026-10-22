using Microsoft.EntityFrameworkCore;

public class GetAssetHistoryHandler(IApplicationDbContext context, ICurrentUser currentUser)
  : IQueryHandler<GetAssetHistoryQuery, Result<GetAssetHistoryQueryResult>>
{
  public async Task<Result<GetAssetHistoryQueryResult>> Handle(GetAssetHistoryQuery query, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(query.AssetId);
    // a deleted asset's history is still read (the record page shows it is deleted); an unknown id is a 404
    if (!await context.Assets.IgnoreQueryFilters().AsNoTracking().AnyAsync(a => a.Id == assetId, cancellationToken))
      throw new AssetNotFoundException("This asset was not found.");

    var page = await AuditQueries.ListAsync(context.AuditRecords, query.Filter with { ParentId = query.AssetId, RecordId = null }, cancellationToken);
    var seesMoney = currentUser.HasPermission(PermissionCatalog.AssetFinance.View);

    var lines = page.Data.Select(line =>
    {
      var restricted = !seesMoney && AssetAuditKinds.Money.Contains(line.RecordKind);
      return new AssetHistoryLineDto(line.Id, line.At, line.ActorId, line.ActorName, line.Action, line.RecordKind, line.RecordId, line.RecordCode,
        line.RecordLabel, line.Field, restricted ? null : line.OldValue, restricted ? null : line.NewValue, restricted ? null : line.Detail,
        line.RequestId, restricted);
    }).ToList();

    return Result<GetAssetHistoryQueryResult>.Success(new GetAssetHistoryQueryResult(new AssetHistoryPageDto(lines, page.Count)));
  }
}
