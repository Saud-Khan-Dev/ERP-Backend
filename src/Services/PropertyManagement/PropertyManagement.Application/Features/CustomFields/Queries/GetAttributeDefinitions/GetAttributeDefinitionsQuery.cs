public sealed record GetAttributeDefinitionsQueryResult(IReadOnlyList<AttributeDefinitionDto> Definitions);

public sealed record GetAttributeDefinitionsQuery(Guid? AttributeGroupId = null, bool IncludeInactive = false) : IQuery<Result<GetAttributeDefinitionsQueryResult>>;
