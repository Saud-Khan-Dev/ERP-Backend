public sealed record GetMasterQueryResult(MasterDto Item);

public sealed record GetMasterQuery(string Type, Guid Id) : IQuery<Result<GetMasterQueryResult>>;
