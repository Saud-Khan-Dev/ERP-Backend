using System.Text.Json;
using FluentValidation;

/// Exactly one of AssetClassId / AssetTypeId / CategoryId / AssetId must be set, matching Scope.
public sealed record AttributeAssignmentInput(
  Guid AttributeDefinitionId,
  AttributeScope Scope,
  Guid? AssetClassId = null,
  Guid? AssetTypeId = null,
  Guid? CategoryId = null,
  Guid? AssetId = null,
  Guid? AttributeGroupId = null,
  string? LabelOverride = null,
  bool IsRequired = false,
  bool IsReadonly = false,
  bool IsSearchable = false,
  bool IsFilterable = false,
  bool IsVisibleInList = false,
  bool InheritToChildren = true,
  JsonElement? DefaultValue = null,
  int? DisplayOrder = null,
  Guid? DependsOnAssignmentId = null,
  JsonElement? DependsOnValue = null,
  bool IsActive = true);

public sealed record CreateAttributeAssignmentCommandResult(Guid Id);

public sealed record CreateAttributeAssignmentCommand(AttributeAssignmentInput Assignment) : ICommand<Result<CreateAttributeAssignmentCommandResult>>;

public class AttributeAssignmentInputValidator : AbstractValidator<AttributeAssignmentInput>
{
  public AttributeAssignmentInputValidator()
  {
    RuleFor(x => x.AttributeDefinitionId).NotEmpty();
    RuleFor(x => x.Scope).IsInEnum();
    RuleFor(x => x.LabelOverride).MaximumLength(150);
    RuleFor(x => x)
      .Must(x => new[] { x.AssetClassId, x.AssetTypeId, x.CategoryId, x.AssetId }.Count(v => v.HasValue && v.Value != Guid.Empty) == 1)
      .WithMessage("Exactly one of assetClassId, assetTypeId, categoryId or assetId must be provided.");
  }
}

public class CreateAttributeAssignmentCommandValidator : AbstractValidator<CreateAttributeAssignmentCommand>
{
  public CreateAttributeAssignmentCommandValidator()
  {
    RuleFor(x => x.Assignment).NotNull().SetValidator(new AttributeAssignmentInputValidator());
  }
}

public static class AttributeAssignmentInputExtensions
{
  public static AttributeAssignmentOptions ToOptions(this AttributeAssignmentInput input) => new(
    input.LabelOverride,
    input.IsRequired,
    input.IsReadonly,
    input.IsSearchable,
    input.IsFilterable,
    input.IsVisibleInList,
    input.InheritToChildren,
    input.DefaultValue,
    input.DisplayOrder,
    input.DependsOnAssignmentId.HasValue ? AttributeAssignmentId.Of(input.DependsOnAssignmentId.Value) : null,
    input.DependsOnValue);
}
