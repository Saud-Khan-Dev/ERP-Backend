public sealed record GetAttributeAssignmentsQueryResult(IReadOnlyList<AttributeAssignmentDto> Assignments);

/// Lists the assignments attached directly to one target (no inheritance). Use ResolveAttributeSchemaQuery for the effective form.
public sealed record GetAttributeAssignmentsQuery(AttributeScope? Scope, Guid? TargetId, Guid? AttributeDefinitionId, bool IncludeInactive)
  : IQuery<Result<GetAttributeAssignmentsQueryResult>>;
