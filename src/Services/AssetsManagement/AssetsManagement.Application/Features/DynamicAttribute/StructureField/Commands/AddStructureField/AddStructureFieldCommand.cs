using System.Text.Json;
using FluentValidation;

/// One choice of a dropdown field. Value is the stored code (derived from the label when left empty).
public sealed record FieldChoiceInput(string Label, string? Value = null);

/// A field that does not exist yet. Code is derived from the label when left empty (Plot facing -> plot_facing).
public sealed record NewFieldInput(
  string Label,
  AttributeDataType DataType,
  string? Code = null,
  string? Description = null,
  string? Unit = null,
  IReadOnlyList<FieldChoiceInput>? Choices = null,
  AttributeValidationRulesDto? Validation = null);

/// How the field appears on this node's form.
public sealed record FieldPlacementInput(
  Guid? SectionId = null,
  string? NewSectionName = null,
  string? LabelOverride = null,
  bool IsRequired = false,
  bool IsVisibleInList = false,
  bool IsFilterable = false,
  bool IsSearchable = false,
  bool InheritToChildren = true,
  JsonElement? DefaultValue = null,
  int? DisplayOrder = null);

public sealed record AddStructureFieldCommandResult(Guid AssignmentId, Guid DefinitionId, Guid? OptionSetId, Guid? SectionId);

/// "Add field" on a node of the asset structure (class, type or category), in ONE transaction: either an existing
/// field from the library is placed here, or a new one is created with its choices - and, when asked, a new form
/// section to put it in. The caller never deals with option sets, definitions and assignments separately.
public sealed record AddStructureFieldCommand(
  AttributeScope Scope,
  Guid TargetId,
  Guid? ExistingDefinitionId,
  NewFieldInput? Field,
  FieldPlacementInput Placement) : ICommand<Result<AddStructureFieldCommandResult>>;

public class AddStructureFieldCommandValidator : AbstractValidator<AddStructureFieldCommand>
{
  public AddStructureFieldCommandValidator()
  {
    RuleFor(x => x.Scope).IsInEnum().Must(s => s != AttributeScope.Asset).WithMessage("Fields are added to a class, a type or a category.");
    RuleFor(x => x.TargetId).NotEmpty();
    RuleFor(x => x).Must(x => (x.ExistingDefinitionId.HasValue) ^ (x.Field is not null))
      .WithMessage("Either choose an existing field or describe a new one.");
    RuleFor(x => x.Placement).NotNull();
    RuleFor(x => x.Placement.LabelOverride).MaximumLength(150);
    RuleFor(x => x.Placement.NewSectionName).MaximumLength(100);
    When(x => x.Field is not null, () =>
    {
      RuleFor(x => x.Field!.Label).NotEmpty().WithMessage("Give the field a name.").MaximumLength(150);
      RuleFor(x => x.Field!.DataType).IsInEnum();
      RuleFor(x => x.Field!.Unit).MaximumLength(50);
      RuleFor(x => x.Field!.Choices)
        .Must(c => c is { Count: > 0 })
        .When(x => x.Field!.DataType is AttributeDataType.Select or AttributeDataType.MultiSelect)
        .WithMessage("A list field needs at least one choice.");
      RuleForEach(x => x.Field!.Choices).ChildRules(c => c.RuleFor(v => v.Label).NotEmpty().MaximumLength(150));
    });
  }
}
