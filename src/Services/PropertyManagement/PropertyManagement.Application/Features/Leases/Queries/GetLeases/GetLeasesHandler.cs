using Microsoft.EntityFrameworkCore;

public class GetLeasesHandler(IApplicationDbContext context, MasterLookup masters, PropertyReadService read)
  : IQueryHandler<GetPropertyLeasesQuery, Result<GetLeasesQueryResult>>,
    IQueryHandler<GetLeaseQuery, Result<GetLeaseQueryResult>>
{
  public async Task<Result<GetLeasesQueryResult>> Handle(GetPropertyLeasesQuery query, CancellationToken cancellationToken)
  {
    var propertyId = await context.EnsurePropertyExistsAsync(query.PropertyId, cancellationToken);
    var rows = await context.Leases.AsNoTracking().Where(l => l.PropertyId == propertyId)
        .OrderByDescending(l => l.LeaseStartDate).ToListAsync(cancellationToken);

    return Result<GetLeasesQueryResult>.Success(new GetLeasesQueryResult(await ToDtosAsync(rows, cancellationToken)));
  }

  public async Task<Result<GetLeaseQueryResult>> Handle(GetLeaseQuery query, CancellationToken cancellationToken)
  {
    var lease = await context.LoadLeaseAsync(query.Id, cancellationToken);
    return Result<GetLeaseQueryResult>.Success(new GetLeaseQueryResult((await ToDtosAsync(new[] { lease }, cancellationToken))[0]));
  }

  private async Task<List<LeaseDto>> ToDtosAsync(IReadOnlyCollection<PropertyLease> rows, CancellationToken cancellationToken)
  {
    var refs = await masters.Refs()
        .Add<LeaseType>(rows.Select(l => l.LeaseTypeId))
        .Add<LeaseStatus>(rows.Select(l => l.LeaseStatusId))
        .LoadAsync(cancellationToken);
    var owners = await read.OwnerRefsAsync(rows.Select(l => l.LesseeOwnerId), cancellationToken);

    return rows.Select(l => l.ToDto(refs, owners)).ToList();
  }
}
