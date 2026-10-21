public sealed record GetPropertyAttributesQueryResult(IReadOnlyList<PropertyAttributeValueDto> Attributes);

/// The custom-field form of a property: the fields that apply to it (value or default), then read-only (IsHidden) the
/// values it holds for fields switched off or no longer applying.
public sealed record GetPropertyAttributesQuery(Guid PropertyId, Guid? AttributeGroupId = null) : IQuery<Result<GetPropertyAttributesQueryResult>>;
