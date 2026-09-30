using Microsoft.EntityFrameworkCore;

public class GetOutsourcingsHandler(IApplicationDbContext context, MasterLookup masters, PropertyReadService read)
  : IQueryHandler<GetPropertyOutsourcingsQuery, Result<GetOutsourcingsQueryResult>>,
    IQueryHandler<GetOutsourcingQuery, Result<GetOutsourcingQueryResult>>
{
  public async Task<Result<GetOutsourcingsQueryResult>> Handle(GetPropertyOutsourcingsQuery query, CancellationToken cancellationToken)
  {
    var propertyId = await context.EnsurePropertyExistsAsync(query.PropertyId, cancellationToken);
    var rows = await context.Outsourcings.AsNoTracking().Where(o => o.PropertyId == propertyId)
        .OrderByDescending(o => o.ContractStartDate).ToListAsync(cancellationToken);

    return Result<GetOutsourcingsQueryResult>.Success(new GetOutsourcingsQueryResult(await ToDtosAsync(rows, cancellationToken)));
  }

  public async Task<Result<GetOutsourcingQueryResult>> Handle(GetOutsourcingQuery query, CancellationToken cancellationToken)
  {
    var contract = await context.LoadOutsourcingAsync(query.Id, cancellationToken);
    return Result<GetOutsourcingQueryResult>.Success(new GetOutsourcingQueryResult((await ToDtosAsync(new[] { contract }, cancellationToken))[0]));
  }

  private async Task<List<OutsourcingDto>> ToDtosAsync(IReadOnlyCollection<PropertyOutsourcing> rows, CancellationToken cancellationToken)
  {
    var refs = await masters.Refs()
        .Add<OutsourcingType>(rows.Select(o => o.OutsourcingTypeId))
        .Add<ContractStatus>(rows.Select(o => o.ContractStatusId))
        .LoadAsync(cancellationToken);
    var owners = await read.OwnerRefsAsync(rows.Select(o => o.OutsourcedPartyOwnerId), cancellationToken);
    return rows.Select(o => o.ToDto(refs, owners)).ToList();
  }
}
