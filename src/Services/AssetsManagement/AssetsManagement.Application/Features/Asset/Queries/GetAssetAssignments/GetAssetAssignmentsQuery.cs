public sealed record GetAssetAssignmentsQueryResult(IReadOnlyList<AssetAssignmentDto> Assignments);

public sealed record GetAssetAssignmentsQuery(Guid AssetId, bool OnlyOpenLoans) : IQuery<Result<GetAssetAssignmentsQueryResult>>;
