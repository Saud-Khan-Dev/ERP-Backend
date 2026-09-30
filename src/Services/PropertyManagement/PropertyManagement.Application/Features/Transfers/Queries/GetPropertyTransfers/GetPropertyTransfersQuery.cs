public sealed record GetPropertyTransfersQueryResult(IReadOnlyList<TransferDto> Transfers);

public sealed record GetPropertyTransfersQuery(Guid PropertyId, TransferStatus? Status = null) : IQuery<Result<GetPropertyTransfersQueryResult>>;
