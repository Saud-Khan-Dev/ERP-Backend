public sealed record GetAttributeDefinitionQueryResult(AttributeDefinitionDto Definition, IReadOnlyList<AttributeAssignmentDto> Assignments);

public sealed record GetAttributeDefinitionQuery(Guid Id) : IQuery<Result<GetAttributeDefinitionQueryResult>>;
