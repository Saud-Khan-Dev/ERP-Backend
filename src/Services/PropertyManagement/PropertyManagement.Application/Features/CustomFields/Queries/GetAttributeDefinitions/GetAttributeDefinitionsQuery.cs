public sealed record GetAttributeDefinitionsQueryResult(IReadOnlyList<AttributeDefinitionDto> Definitions);

/// PropertyTypeId + ClassificationId: only the fields that apply to such a property, are switched on and whose group is
/// switched on - what a registration form shows.
public sealed record GetAttributeDefinitionsQuery(Guid? AttributeGroupId = null, bool IncludeInactive = false, Guid? PropertyTypeId = null, Guid? ClassificationId = null)
  : IQuery<Result<GetAttributeDefinitionsQueryResult>>;
