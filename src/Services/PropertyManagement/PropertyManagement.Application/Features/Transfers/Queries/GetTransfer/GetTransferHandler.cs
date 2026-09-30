public class GetTransferHandler(IApplicationDbContext context, TransferReader reader)
  : IQueryHandler<GetTransferQuery, Result<GetTransferQueryResult>>
{
  public async Task<Result<GetTransferQueryResult>> Handle(GetTransferQuery query, CancellationToken cancellationToken)
  {
    var transfer = await context.LoadTransferAsync(query.Id, cancellationToken);
    var dtos = await reader.ToDtosAsync(new[] { transfer }, cancellationToken);

    return Result<GetTransferQueryResult>.Success(new GetTransferQueryResult(dtos[0]));
  }
}

/// Resolves masters, owners and the property for a batch of transfers.
public class TransferReader(MasterLookup masters, PropertyReadService read)
{
  public async Task<List<TransferDto>> ToDtosAsync(IReadOnlyCollection<PropertyTransfer> transfers, CancellationToken cancellationToken)
  {
    var refs = await masters.Refs().Add<TransferType>(transfers.Select(t => t.TransferTypeId)).LoadAsync(cancellationToken);
    var owners = await read.OwnerRefsAsync(transfers.SelectMany(t => t.Parties).Select(p => p.OwnerId), cancellationToken);
    var properties = await read.PropertyRefsAsync(transfers.Select(t => t.PropertyId), cancellationToken);

    return transfers.Select(t => t.ToDto(refs, properties.GetValueOrDefault(t.PropertyId), owners)).ToList();
  }
}
