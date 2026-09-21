using System.Text.Json;

public sealed record OptionSetValueDto(
  Guid Id,
  Guid OptionSetId,
  Guid? ParentValueId,
  string Value,
  string Label,
  string? Color,
  string? Icon,
  int? DisplayOrder,
  bool IsActive);

public sealed record OptionSetDto(
  Guid Id,
  string Code,
  string Name,
  string? Description,
  bool IsSystem,
  bool IsActive,
  IReadOnlyList<OptionSetValueDto> Values);

public sealed record AttributeGroupDto(
  Guid Id,
  string Code,
  string Name,
  string? Description,
  int? DisplayOrder,
  bool IsCollapsible,
  bool IsActive);

public sealed record AttributeValidationRulesDto(
  decimal? MinNumber,
  decimal? MaxNumber,
  int? MinLength,
  int? MaxLength,
  DateOnly? MinDate,
  DateOnly? MaxDate,
  string? RegexPattern,
  bool IsUniquePerCategory,
  string? ValidationMessage);

public sealed record AttributeDefinitionDto(
  Guid Id,
  string Code,
  string Name,
  string? Description,
  AttributeDataType DataType,
  Guid? OptionSetId,
  string? ReferenceEntity,
  string? Unit,
  int? NumericPrecision,
  int? NumericScale,
  AttributeValidationRulesDto Validation,
  bool IsMultiValue,
  bool IsPii,
  bool IsSystem,
  bool IsActive);

public sealed record AttributeAssignmentDto(
  Guid Id,
  Guid AttributeDefinitionId,
  AttributeScope Scope,
  Guid? AssetClassId,
  Guid? AssetTypeId,
  Guid? CategoryId,
  Guid? AssetId,
  Guid? AttributeGroupId,
  string? LabelOverride,
  bool IsRequired,
  bool IsReadonly,
  bool IsSearchable,
  bool IsFilterable,
  bool IsVisibleInList,
  bool InheritToChildren,
  JsonElement? DefaultValue,
  int? DisplayOrder,
  Guid? DependsOnAssignmentId,
  JsonElement? DependsOnValue,
  bool IsActive);

/// One field of the resolved form an asset (or a category / type / class) exposes for data entry.
public sealed record ResolvedAttributeDto(
  Guid AttributeDefinitionId,
  Guid AssignmentId,
  string Code,
  string Label,
  string? Description,
  AttributeDataType DataType,
  AttributeScope ResolvedFrom,
  Guid? AttributeGroupId,
  string? Unit,
  bool IsRequired,
  bool IsReadonly,
  bool IsSearchable,
  bool IsFilterable,
  bool IsVisibleInList,
  bool IsMultiValue,
  int? DisplayOrder,
  JsonElement? DefaultValue,
  Guid? DependsOnAssignmentId,
  JsonElement? DependsOnValue,
  AttributeValidationRulesDto Validation,
  Guid? OptionSetId,
  IReadOnlyList<OptionSetValueDto> Options);

public sealed record AssetAttributeHistoryDto(
  Guid Id,
  Guid AssetId,
  Guid AttributeDefinitionId,
  string AttributeCode,
  JsonElement? OldValue,
  JsonElement? NewValue,
  DateTime ChangedAt,
  Guid? ChangedBy,
  string? ChangeReason);

public static class DynamicAttributeMappings
{
  public static OptionSetValueDto ToDto(this OptionSetValue x) => new(
    x.Id.Value, x.OptionSetId.Value, x.ParentValueId?.Value, x.Value.Value, x.Label,
    x.Color, x.Icon, x.DisplayOrder, x.IsActive);

  public static OptionSetDto ToDto(this OptionSet x) => new(
    x.Id.Value, x.Code.Value, x.Label.Value, x.Description, x.IsSystem, x.IsActive,
    x.Values.OrderBy(v => v.DisplayOrder ?? int.MaxValue).ThenBy(v => v.Label).Select(v => v.ToDto()).ToList());

  public static AttributeGroupDto ToDto(this AttributeGroup x) => new(
    x.Id.Value, x.Code.Value, x.Name.Value, x.Description, x.DisplayOrder, x.IsCollapsible, x.IsActive);

  public static AttributeValidationRulesDto ToValidationDto(this AttributeDefinition x) => new(
    x.MinNumber, x.MaxNumber, x.MinLength, x.MaxLength, x.MinDate, x.MaxDate,
    x.RegexPattern, x.IsUniquePerCategory, x.ValidationMessage);

  public static AttributeDefinitionDto ToDto(this AttributeDefinition x) => new(
    x.Id.Value, x.Code.Value, x.Name.Value, x.Description, x.DataType, x.OptionSetId?.Value,
    x.ReferenceEntity, x.Unit, x.NumericPrecision, x.NumericScale, x.ToValidationDto(),
    x.IsMultiValue, x.IsPii, x.IsSystem, x.IsActive);

  public static AttributeAssignmentDto ToDto(this AttributeAssignment x) => new(
    x.Id.Value, x.AttributeDefinitionId.Value, x.Scope, x.AssetClassId?.Value, x.AssetTypeId?.Value,
    x.CategoryId?.Value, x.AssetId?.Value, x.AttributeGroupId?.Value, x.LabelOverride,
    x.IsRequired, x.IsReadonly, x.IsSearchable, x.IsFilterable, x.IsVisibleInList, x.InheritToChildren,
    x.DefaultValue, x.DisplayOrder, x.DependsOnAssignmentId?.Value, x.DependsOnValue, x.IsActive);

  public static ResolvedAttributeDto ToDto(this ResolvedAttribute x, OptionSet? optionSet)
  {
    var definition = x.Definition;
    var assignment = x.Assignment;

    return new ResolvedAttributeDto(
      definition.Id.Value,
      assignment.Id.Value,
      x.Code,
      x.Label,
      definition.Description,
      definition.DataType,
      x.ResolvedFrom,
      assignment.AttributeGroupId?.Value,
      definition.Unit,
      assignment.IsRequired,
      assignment.IsReadonly,
      assignment.IsSearchable,
      assignment.IsFilterable,
      assignment.IsVisibleInList,
      definition.IsMultiValue,
      assignment.DisplayOrder,
      assignment.DefaultValue,
      assignment.DependsOnAssignmentId?.Value,
      assignment.DependsOnValue,
      definition.ToValidationDto(),
      definition.OptionSetId?.Value,
      optionSet is null
        ? Array.Empty<OptionSetValueDto>()
        : optionSet.Values.Where(v => v.IsActive).OrderBy(v => v.DisplayOrder ?? int.MaxValue).Select(v => v.ToDto()).ToList());
  }

  public static AssetAttributeHistoryDto ToDto(this AssetAttributeHistory x) => new(
    x.Id.Value, x.AssetId.Value, x.AttributeDefinitionId.Value, x.AttributeCode,
    x.OldValue, x.NewValue, x.ChangedAt, x.ChangedBy, x.ChangeReason);
}
