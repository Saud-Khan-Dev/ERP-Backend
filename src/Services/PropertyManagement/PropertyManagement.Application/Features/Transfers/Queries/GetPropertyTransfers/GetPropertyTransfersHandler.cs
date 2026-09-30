using Microsoft.EntityFrameworkCore;

public class GetPropertyTransfersHandler(IApplicationDbContext context, TransferReader reader)
  : IQueryHandler<GetPropertyTransfersQuery, Result<GetPropertyTransfersQueryResult>>
{
  public async Task<Result<GetPropertyTransfersQueryResult>> Handle(GetPropertyTransfersQuery query, CancellationToken cancellationToken)
  {
    var propertyId = PropertyId.Of(query.PropertyId);

    if (!await context.Properties.AnyAsync(p => p.Id == propertyId, cancellationToken))
      throw new PropertyNotFoundException($"Property {query.PropertyId} was not found.");

    var transfers = context.Transfers.AsNoTracking().Include(t => t.Parties).Where(t => t.PropertyId == propertyId);

    if (query.Status is { } status)
      transfers = transfers.Where(t => t.TransferStatus == status);

    var rows = await transfers.OrderByDescending(t => t.TransferDate).ThenByDescending(t => t.CreatedAt).ToListAsync(cancellationToken);

    return Result<GetPropertyTransfersQueryResult>.Success(
      new GetPropertyTransfersQueryResult(await reader.ToDtosAsync(rows, cancellationToken)));
  }
}
