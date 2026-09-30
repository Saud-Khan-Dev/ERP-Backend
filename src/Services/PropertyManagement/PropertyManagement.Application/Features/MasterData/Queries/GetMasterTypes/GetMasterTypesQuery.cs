public sealed record GetMasterTypesQueryResult(IReadOnlyList<MasterTypeDto> Types);

public sealed record GetMasterTypesQuery : IQuery<Result<GetMasterTypesQueryResult>>;
