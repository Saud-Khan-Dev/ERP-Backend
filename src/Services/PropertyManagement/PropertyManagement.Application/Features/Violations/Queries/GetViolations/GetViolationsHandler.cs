using Microsoft.EntityFrameworkCore;

public class GetViolationsHandler(IApplicationDbContext context, MasterLookup masters, PropertyReadService read)
  : IQueryHandler<GetPropertyViolationsQuery, Result<GetViolationsQueryResult>>,
    IQueryHandler<GetViolationQuery, Result<GetViolationQueryResult>>
{
  public async Task<Result<GetViolationsQueryResult>> Handle(GetPropertyViolationsQuery query, CancellationToken cancellationToken)
  {
    var propertyId = await context.EnsurePropertyExistsAsync(query.PropertyId, cancellationToken);
    var rows = context.Violations.AsNoTracking().Where(v => v.PropertyId == propertyId);

    if (query.LeaseId is { } leaseId) rows = rows.Where(v => v.LeaseId == LeaseId.Of(leaseId));
    if (query.RentalId is { } rentalId) rows = rows.Where(v => v.RentalId == RentalId.Of(rentalId));
    if (query.TransferId is { } transferId) rows = rows.Where(v => v.TransferId == TransferId.Of(transferId));

    var list = await rows.OrderByDescending(v => v.ViolationDate).ThenByDescending(v => v.OccurrenceNo).ToListAsync(cancellationToken);
    return Result<GetViolationsQueryResult>.Success(new GetViolationsQueryResult(await ToDtosAsync(list, cancellationToken)));
  }

  public async Task<Result<GetViolationQueryResult>> Handle(GetViolationQuery query, CancellationToken cancellationToken)
  {
    var violation = await context.LoadViolationAsync(query.Id, cancellationToken);
    return Result<GetViolationQueryResult>.Success(new GetViolationQueryResult((await ToDtosAsync(new[] { violation }, cancellationToken))[0]));
  }

  private async Task<List<ViolationDto>> ToDtosAsync(IReadOnlyCollection<AgreementViolation> rows, CancellationToken cancellationToken)
  {
    var refs = await masters.Refs().Add<AgreementType>(rows.Select(v => v.AgreementTypeId)).LoadAsync(cancellationToken);
    var owners = await read.OwnerRefsAsync(rows.Select(v => v.ViolatorOwnerId), cancellationToken);
    return rows.Select(v => v.ToDto(refs, owners)).ToList();
  }
}
