public sealed record GetPropertyAttributesQueryResult(IReadOnlyList<PropertyAttributeValueDto> Attributes);

/// The custom-field form of a property: every active field with the value captured (or its default).
public sealed record GetPropertyAttributesQuery(Guid PropertyId, Guid? AttributeGroupId = null) : IQuery<Result<GetPropertyAttributesQueryResult>>;
