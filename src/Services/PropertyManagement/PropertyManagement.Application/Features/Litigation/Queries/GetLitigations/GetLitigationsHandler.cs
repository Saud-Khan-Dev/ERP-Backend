using Microsoft.EntityFrameworkCore;

public class GetLitigationsHandler(IApplicationDbContext context, MasterLookup masters, PropertyReadService read)
  : IQueryHandler<GetPropertyLitigationsQuery, Result<GetLitigationsQueryResult>>,
    IQueryHandler<GetLitigationQuery, Result<GetLitigationQueryResult>>,
    IQueryHandler<GetUpcomingHearingsQuery, Result<GetLitigationsQueryResult>>
{
  public async Task<Result<GetLitigationsQueryResult>> Handle(GetPropertyLitigationsQuery query, CancellationToken cancellationToken)
  {
    var propertyId = await context.EnsurePropertyExistsAsync(query.PropertyId, cancellationToken);
    var rows = await WithChildren().Where(l => l.PropertyId == propertyId)
        .OrderByDescending(l => l.FilingDate).ThenByDescending(l => l.CreatedAt).ToListAsync(cancellationToken);

    return Result<GetLitigationsQueryResult>.Success(new GetLitigationsQueryResult(await ToDtosAsync(rows, cancellationToken)));
  }

  public async Task<Result<GetLitigationQueryResult>> Handle(GetLitigationQuery query, CancellationToken cancellationToken)
  {
    var litigation = await context.LoadLitigationAsync(query.Id, cancellationToken);
    return Result<GetLitigationQueryResult>.Success(new GetLitigationQueryResult((await ToDtosAsync(new[] { litigation }, cancellationToken))[0]));
  }

  public async Task<Result<GetLitigationsQueryResult>> Handle(GetUpcomingHearingsQuery query, CancellationToken cancellationToken)
  {
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var until = today.AddDays(Math.Clamp(query.Days, 1, 365));

    var rows = await WithChildren()
        .Where(l => l.NextHearingDate != null && l.NextHearingDate >= today && l.NextHearingDate <= until)
        .OrderBy(l => l.NextHearingDate).ToListAsync(cancellationToken);

    return Result<GetLitigationsQueryResult>.Success(new GetLitigationsQueryResult(await ToDtosAsync(rows, cancellationToken)));
  }

  private IQueryable<PropertyLitigation> WithChildren() =>
      context.Litigations.AsNoTracking().Include(l => l.Parties).Include(l => l.Hearings);

  private async Task<List<LitigationDto>> ToDtosAsync(IReadOnlyCollection<PropertyLitigation> rows, CancellationToken cancellationToken)
  {
    var refs = await masters.Refs()
        .Add<LitigationType>(rows.Select(l => l.LitigationTypeId))
        .Add<LitigationStatus>(rows.Select(l => l.LitigationStatusId))
        .LoadAsync(cancellationToken);
    var owners = await read.OwnerRefsAsync(rows.SelectMany(l => l.Parties).Select(p => p.PartyOwnerId), cancellationToken);
    var properties = await read.PropertyRefsAsync(rows.Select(l => l.PropertyId), cancellationToken);
    return rows.Select(l => l.ToDto(refs, owners, properties.GetValueOrDefault(l.PropertyId))).ToList();
  }
}

