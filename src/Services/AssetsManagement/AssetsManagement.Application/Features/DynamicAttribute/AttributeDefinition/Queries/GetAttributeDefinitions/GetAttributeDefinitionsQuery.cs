public sealed record GetAttributeDefinitionsQueryResult(IReadOnlyList<AttributeDefinitionDto> Definitions);

public sealed record GetAttributeDefinitionsQuery(AttributeDataType? DataType, string? Search, bool IncludeInactive)
  : IQuery<Result<GetAttributeDefinitionsQueryResult>>;
