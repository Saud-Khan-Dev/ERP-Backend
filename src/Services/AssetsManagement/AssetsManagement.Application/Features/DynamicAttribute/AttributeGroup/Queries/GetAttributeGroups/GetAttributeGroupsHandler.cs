using Microsoft.EntityFrameworkCore;

public class GetAttributeGroupsHandler(IApplicationDbContext context)
  : IQueryHandler<GetAttributeGroupsQuery, Result<GetAttributeGroupsQueryResult>>
{
  public async Task<Result<GetAttributeGroupsQueryResult>> Handle(GetAttributeGroupsQuery query, CancellationToken cancellationToken)
  {
    var groups = context.AttributeGroups.AsNoTracking();

    if (!query.IncludeInactive)
      groups = groups.Where(g => g.IsActive);

    var data = await groups.OrderBy(g => g.DisplayOrder).ThenBy(g => g.Code).ToListAsync(cancellationToken);

    return Result<GetAttributeGroupsQueryResult>.Success(new GetAttributeGroupsQueryResult(data.Select(g => g.ToDto()).ToList()));
  }
}
