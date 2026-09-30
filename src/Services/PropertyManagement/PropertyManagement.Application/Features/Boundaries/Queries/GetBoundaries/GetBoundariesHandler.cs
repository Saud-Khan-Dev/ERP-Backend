using Microsoft.EntityFrameworkCore;

public class GetBoundariesHandler(IApplicationDbContext context)
  : IQueryHandler<GetPropertyBoundariesQuery, Result<GetBoundariesQueryResult>>,
    IQueryHandler<GetBoundaryQuery, Result<GetBoundaryQueryResult>>
{
  public async Task<Result<GetBoundariesQueryResult>> Handle(GetPropertyBoundariesQuery query, CancellationToken cancellationToken)
  {
    var propertyId = await context.EnsurePropertyExistsAsync(query.PropertyId, cancellationToken);

    var rows = await context.Boundaries.AsNoTracking().Include(b => b.Points)
        .Where(b => b.PropertyId == propertyId && (!query.CurrentOnly || b.IsCurrent))
        .OrderByDescending(b => b.IsCurrent).ThenByDescending(b => b.CreatedAt)
        .ToListAsync(cancellationToken);

    return Result<GetBoundariesQueryResult>.Success(new GetBoundariesQueryResult(rows.Select(b => b.ToDto()).ToList()));
  }

  public async Task<Result<GetBoundaryQueryResult>> Handle(GetBoundaryQuery query, CancellationToken cancellationToken)
  {
    var boundary = await context.LoadBoundaryAsync(query.Id, cancellationToken);
    return Result<GetBoundaryQueryResult>.Success(new GetBoundaryQueryResult(boundary.ToDto()));
  }
}
