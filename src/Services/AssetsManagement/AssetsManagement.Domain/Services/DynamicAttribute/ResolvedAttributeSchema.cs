/// One effective field of an asset's form: the definition plus the assignment that won the resolution.
public sealed record ResolvedAttribute(AttributeDefinition Definition, AttributeAssignment Assignment, AttributeScope ResolvedFrom)
{
  public string Code => Definition.Code.Value;
  public string Label => Assignment.LabelOverride ?? Definition.Name.Value;
  public bool IsProjected => Assignment.IsProjected || Definition.IsUniquePerCategory;
}

/// The resolved attribute set for a (class, type, category chain, asset) tuple.
public sealed class ResolvedAttributeSchema
{
  private readonly Dictionary<string, ResolvedAttribute> _byCode;
  private readonly Dictionary<AttributeAssignmentId, AttributeAssignment> _assignmentsById;

  public IReadOnlyList<ResolvedAttribute> Attributes { get; }

  internal ResolvedAttributeSchema(IReadOnlyList<ResolvedAttribute> attributes, IEnumerable<AttributeAssignment> participatingAssignments)
  {
    Attributes = attributes;
    _byCode = attributes.ToDictionary(a => a.Code, StringComparer.Ordinal);
    _assignmentsById = participatingAssignments
        .GroupBy(a => a.Id)
        .ToDictionary(g => g.Key, g => g.First());
  }

  public static ResolvedAttributeSchema Empty { get; } = new(Array.Empty<ResolvedAttribute>(), Array.Empty<AttributeAssignment>());

  public ResolvedAttribute? Find(string code) => _byCode.GetValueOrDefault(code);

  public bool Contains(string code) => _byCode.ContainsKey(code);

  /// depends_on_assignment_id points at *an* assignment of the parent attribute; the effective one may be an override,
  /// so resolve through the definition rather than the assignment id itself.
  public ResolvedAttribute? FindByAssignmentId(AttributeAssignmentId assignmentId)
  {
    if (!_assignmentsById.TryGetValue(assignmentId, out var assignment))
      return null;

    return Attributes.FirstOrDefault(a => a.Definition.Id == assignment.AttributeDefinitionId);
  }
}
