using Microsoft.EntityFrameworkCore;

public class GetAssetAssignmentsHandler(IApplicationDbContext context)
  : IQueryHandler<GetAssetAssignmentsQuery, Result<GetAssetAssignmentsQueryResult>>
{
  public async Task<Result<GetAssetAssignmentsQueryResult>> Handle(GetAssetAssignmentsQuery query, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(query.AssetId);
    if (!await context.Assets.IgnoreQueryFilters().AnyAsync(a => a.Id == assetId, cancellationToken))
      throw new AssetNotFoundException($"Asset {query.AssetId} was not found.");

    var assignments = context.AssetAssignments.AsNoTracking().Where(a => a.AssetId == assetId);

    if (query.OnlyOpenLoans)
      assignments = assignments.Where(a => a.ExpectedReturnDate != null && a.ActualReturnDate == null);

    var data = await assignments.OrderByDescending(a => a.AssignmentDate).ToListAsync(cancellationToken);

    return Result<GetAssetAssignmentsQueryResult>.Success(new GetAssetAssignmentsQueryResult(data.Select(a => a.ToDto()).ToList()));
  }
}
