public sealed record GetAttributeGroupsQueryResult(IReadOnlyList<AttributeGroupDto> Groups);

public sealed record GetAttributeGroupsQuery(bool IncludeInactive) : IQuery<Result<GetAttributeGroupsQueryResult>>;
