public sealed record GetTransferQueryResult(TransferDto Transfer);

public sealed record GetTransferQuery(Guid Id) : IQuery<Result<GetTransferQueryResult>>;
