using System.Text.Json;
using FluentValidation;

public sealed record UpdateAttributeAssignmentCommandResult(bool IsSuccess);

/// Only the per-assignment overrides can change; the definition and target are fixed (delete + recreate to move).
public sealed record UpdateAttributeAssignmentCommand(
  Guid Id,
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
  bool IsActive) : ICommand<Result<UpdateAttributeAssignmentCommandResult>>;

public class UpdateAttributeAssignmentCommandValidator : AbstractValidator<UpdateAttributeAssignmentCommand>
{
  public UpdateAttributeAssignmentCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.LabelOverride).MaximumLength(150);
  }
}
