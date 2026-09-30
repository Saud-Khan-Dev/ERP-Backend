using Microsoft.EntityFrameworkCore;

public class GetEncroachmentsHandler(IApplicationDbContext context, MasterLookup masters, PropertyReadService read)
  : IQueryHandler<GetPropertyEncroachmentsQuery, Result<GetEncroachmentsQueryResult>>,
    IQueryHandler<GetEncroachmentQuery, Result<GetEncroachmentQueryResult>>
{
  public async Task<Result<GetEncroachmentsQueryResult>> Handle(GetPropertyEncroachmentsQuery query, CancellationToken cancellationToken)
  {
    var propertyId = await context.EnsurePropertyExistsAsync(query.PropertyId, cancellationToken);

    var rows = await context.Encroachments.AsNoTracking().Include(e => e.Points)
        .Where(e => e.PropertyId == propertyId && (!query.UnresolvedOnly || e.ResolutionDate == null))
        .OrderByDescending(e => e.DetectionDate)
        .ToListAsync(cancellationToken);

    return Result<GetEncroachmentsQueryResult>.Success(new GetEncroachmentsQueryResult(await ToDtosAsync(rows, cancellationToken)));
  }

  public async Task<Result<GetEncroachmentQueryResult>> Handle(GetEncroachmentQuery query, CancellationToken cancellationToken)
  {
    var encroachment = await context.LoadEncroachmentAsync(query.Id, cancellationToken);
    return Result<GetEncroachmentQueryResult>.Success(new GetEncroachmentQueryResult((await ToDtosAsync(new[] { encroachment }, cancellationToken))[0]));
  }

  private async Task<List<EncroachmentDto>> ToDtosAsync(IReadOnlyCollection<PropertyEncroachment> rows, CancellationToken cancellationToken)
  {
    var refs = await masters.Refs()
        .Add<MeasurementUnit>(rows.Select(e => e.MeasurementUnitId))
        .Add<EncroachmentStatus>(rows.Select(e => e.EncroachmentStatusId))
        .LoadAsync(cancellationToken);
    var owners = await read.OwnerRefsAsync(rows.Select(e => e.EncroacherOwnerId), cancellationToken);
    return rows.Select(e => e.ToDto(refs, owners)).ToList();
  }
}
