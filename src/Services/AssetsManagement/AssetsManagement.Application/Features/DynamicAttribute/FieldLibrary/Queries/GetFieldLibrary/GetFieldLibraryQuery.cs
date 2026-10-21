/// Where a field is placed: on which class, type or category, and how.
public sealed record FieldPlacementDto(
  Guid AssignmentId,
  AttributeScope Scope,
  Guid TargetId,
  bool IsActive,
  bool IsHidden,
  bool IsRequired,
  bool IsFilterable,
  bool IsSearchable,
  bool IsVisibleInList,
  bool InheritToChildren,
  string? LabelOverride);

/// One field of the library: what it is, its choices (switched-off ones too), where it is placed and how many assets
/// (not deleted) hold a value for it - what decides whether it can be deleted.
public sealed record FieldLibraryItemDto(
  AttributeDefinitionDto Definition,
  IReadOnlyList<OptionSetValueDto> Choices,
  IReadOnlyList<FieldPlacementDto> Placements,
  int AssetsHolding,
  /// Delete is possible: no asset holds a value and the system does not rely on it.
  bool CanDelete);

public sealed record GetFieldLibraryQueryResult(IReadOnlyList<FieldLibraryItemDto> Fields);

/// Configuration › Fields: every field (switched-off ones too) with its usage.
public sealed record GetFieldLibraryQuery : IQuery<Result<GetFieldLibraryQueryResult>>;
