using Microsoft.EntityFrameworkCore;

public class GetRentalsHandler(IApplicationDbContext context, MasterLookup masters, PropertyReadService read)
  : IQueryHandler<GetPropertyRentalsQuery, Result<GetRentalsQueryResult>>,
    IQueryHandler<GetRentalQuery, Result<GetRentalQueryResult>>
{
  public async Task<Result<GetRentalsQueryResult>> Handle(GetPropertyRentalsQuery query, CancellationToken cancellationToken)
  {
    var propertyId = await context.EnsurePropertyExistsAsync(query.PropertyId, cancellationToken);
    var rows = await context.Rentals.AsNoTracking().Where(r => r.PropertyId == propertyId)
        .OrderByDescending(r => r.RentalStartDate).ToListAsync(cancellationToken);

    return Result<GetRentalsQueryResult>.Success(new GetRentalsQueryResult(await ToDtosAsync(rows, cancellationToken)));
  }

  public async Task<Result<GetRentalQueryResult>> Handle(GetRentalQuery query, CancellationToken cancellationToken)
  {
    var rental = await context.LoadRentalAsync(query.Id, cancellationToken);
    return Result<GetRentalQueryResult>.Success(new GetRentalQueryResult((await ToDtosAsync(new[] { rental }, cancellationToken))[0]));
  }

  private async Task<List<RentalDto>> ToDtosAsync(IReadOnlyCollection<PropertyRental> rows, CancellationToken cancellationToken)
  {
    var refs = await masters.Refs()
        .Add<RentalType>(rows.Select(r => r.RentalTypeId))
        .Add<RentalStatus>(rows.Select(r => r.RentalStatusId))
        .LoadAsync(cancellationToken);
    var owners = await read.OwnerRefsAsync(rows.Select(r => r.TenantOwnerId), cancellationToken);

    return rows.Select(r => r.ToDto(refs, owners)).ToList();
  }
}
