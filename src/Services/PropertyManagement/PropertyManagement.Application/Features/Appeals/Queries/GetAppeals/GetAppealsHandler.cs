using Microsoft.EntityFrameworkCore;

public class GetAppealsHandler(IApplicationDbContext context, PropertyReadService read)
  : IQueryHandler<GetPropertyAppealsQuery, Result<GetAppealsQueryResult>>,
    IQueryHandler<GetAppealQuery, Result<GetAppealQueryResult>>,
    IQueryHandler<GetOverdueAppealsQuery, Result<GetAppealsQueryResult>>
{
  public async Task<Result<GetAppealsQueryResult>> Handle(GetPropertyAppealsQuery query, CancellationToken cancellationToken)
  {
    var propertyId = await context.EnsurePropertyExistsAsync(query.PropertyId, cancellationToken);
    var rows = await context.Appeals.AsNoTracking().Where(a => a.PropertyId == propertyId)
        .OrderByDescending(a => a.AppealDate).ToListAsync(cancellationToken);

    return Result<GetAppealsQueryResult>.Success(new GetAppealsQueryResult(await ToDtosAsync(rows, cancellationToken)));
  }

  public async Task<Result<GetAppealQueryResult>> Handle(GetAppealQuery query, CancellationToken cancellationToken)
  {
    var appeal = await context.LoadAppealAsync(query.Id, cancellationToken);
    return Result<GetAppealQueryResult>.Success(new GetAppealQueryResult((await ToDtosAsync(new[] { appeal }, cancellationToken))[0]));
  }

  public async Task<Result<GetAppealsQueryResult>> Handle(GetOverdueAppealsQuery query, CancellationToken cancellationToken)
  {
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var rows = await context.Appeals.AsNoTracking()
        .Where(a => (a.AppealStatus == AppealStatus.Filed || a.AppealStatus == AppealStatus.UnderHearing) && a.DecisionDueDate < today)
        .OrderBy(a => a.DecisionDueDate).ToListAsync(cancellationToken);

    return Result<GetAppealsQueryResult>.Success(new GetAppealsQueryResult(await ToDtosAsync(rows, cancellationToken)));
  }

  private async Task<List<AppealDto>> ToDtosAsync(IReadOnlyCollection<PropertyAppeal> rows, CancellationToken cancellationToken)
  {
    var owners = await read.OwnerRefsAsync(rows.Select(a => a.AppellantOwnerId), cancellationToken);
    var properties = await read.PropertyRefsAsync(rows.Select(a => a.PropertyId), cancellationToken);
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    return rows.Select(a => a.ToDto(owners, properties.GetValueOrDefault(a.PropertyId), today)).ToList();
  }
}
