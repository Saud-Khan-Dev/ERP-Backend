using Microsoft.EntityFrameworkCore;

public class GetAttributeAssignmentsHandler(IApplicationDbContext context)
  : IQueryHandler<GetAttributeAssignmentsQuery, Result<GetAttributeAssignmentsQueryResult>>
{
  public async Task<Result<GetAttributeAssignmentsQueryResult>> Handle(GetAttributeAssignmentsQuery query, CancellationToken cancellationToken)
  {
    var assignments = context.AttributeAssignments.AsNoTracking();

    if (query.Scope.HasValue)
      assignments = assignments.Where(a => a.Scope == query.Scope.Value);

    if (query.Scope.HasValue && query.TargetId.HasValue)
    {
      var targetId = query.TargetId.Value;
      assignments = query.Scope.Value switch
      {
        AttributeScope.AssetClass => assignments.Where(a => a.AssetClassId == AssetClassId.Of(targetId)),
        AttributeScope.AssetType => assignments.Where(a => a.AssetTypeId == AssetTypeId.Of(targetId)),
        AttributeScope.Category => assignments.Where(a => a.CategoryId == AssetCategoryId.Of(targetId)),
        AttributeScope.Asset => assignments.Where(a => a.AssetId == AssetId.Of(targetId)),
        _ => assignments
      };
    }

    if (query.AttributeDefinitionId.HasValue)
    {
      var definitionId = AttributeDefinitionId.Of(query.AttributeDefinitionId.Value);
      assignments = assignments.Where(a => a.AttributeDefinitionId == definitionId);
    }

    if (!query.IncludeInactive)
      assignments = assignments.Where(a => a.IsActive);

    var data = await assignments.OrderBy(a => a.DisplayOrder).ToListAsync(cancellationToken);

    return Result<GetAttributeAssignmentsQueryResult>.Success(new GetAttributeAssignmentsQueryResult(data.Select(a => a.ToDto()).ToList()));
  }
}
