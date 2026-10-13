using Microsoft.EntityFrameworkCore;

public class GetAllotmentsHandler(IApplicationDbContext context, MasterLookup masters, PropertyReadService read)
  : IQueryHandler<GetPropertyAllotmentsQuery, Result<GetAllotmentsQueryResult>>,
    IQueryHandler<GetAllotmentQuery, Result<GetAllotmentQueryResult>>
{
  public async Task<Result<GetAllotmentsQueryResult>> Handle(GetPropertyAllotmentsQuery query, CancellationToken cancellationToken)
  {
    var propertyId = await context.EnsurePropertyExistsAsync(query.PropertyId, cancellationToken);

    var rows = await context.Allotments.AsNoTracking()
        .Where(a => a.PropertyId == propertyId && (query.IncludeInactive || a.IsActive))
        .OrderByDescending(a => a.AllotmentDate)
        .ToListAsync(cancellationToken);

    return Result<GetAllotmentsQueryResult>.Success(new GetAllotmentsQueryResult(await ToDtosAsync(rows, cancellationToken)));
  }

  public async Task<Result<GetAllotmentQueryResult>> Handle(GetAllotmentQuery query, CancellationToken cancellationToken)
  {
    var allotment = await context.LoadAllotmentAsync(query.Id, cancellationToken);
    return Result<GetAllotmentQueryResult>.Success(new GetAllotmentQueryResult((await ToDtosAsync(new[] { allotment }, cancellationToken))[0]));
  }

  private async Task<List<AllotmentDto>> ToDtosAsync(IReadOnlyCollection<PropertyAllotment> rows, CancellationToken cancellationToken)
  {
    var refs = await masters.Refs()
        .Add<AllotmentType>(rows.Select(a => a.AllotmentTypeId))
        .Add<AllotmentStatus>(rows.Select(a => a.AllotmentStatusId))
        .LoadAsync(cancellationToken);
    var owners = await read.OwnerRefsAsync(rows.Select(a => a.AllotteeOwnerId), cancellationToken);
    var inForce = await context.InForceAllotmentStatusIdsAsync(cancellationToken);

    var ids = rows.Select(a => (AllotmentId?)a.Id).ToList();
    var confirmations = (await context.Ownerships.AsNoTracking()
        .Where(o => o.AcquiredViaAllotmentId != null && ids.Contains(o.AcquiredViaAllotmentId))
        .ToListAsync(cancellationToken))
      .ToLookup(o => o.AcquiredViaAllotmentId!);

    return rows.Select(a => a.ToDto(refs, owners,
        a.IsActive && inForce.Contains(a.AllotmentStatusId),
        a.ConfirmedOwnership(confirmations[a.Id])?.Id.Value)).ToList();
  }
}
