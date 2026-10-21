using FluentValidation;

public sealed record HideStructureFieldCommandResult(Guid AssignmentId);

/// "Not used here": a field a level above adds (a class field on a type, a type field on a category …) is left off
/// this node's form - and, with InheritToChildren, off everything below it. Values assets already hold stay on them.
public sealed record HideStructureFieldCommand(AttributeScope Scope, Guid TargetId, Guid DefinitionId, bool InheritToChildren = true)
  : ICommand<Result<HideStructureFieldCommandResult>>;

/// "Use here again": takes the "not used here" mark off.
public sealed record UnhideStructureFieldCommand(Guid AssignmentId) : ICommand<Result<HideStructureFieldCommandResult>>;

public class HideStructureFieldCommandValidator : AbstractValidator<HideStructureFieldCommand>
{
  public HideStructureFieldCommandValidator()
  {
    RuleFor(x => x.Scope).IsInEnum().Must(s => s is AttributeScope.AssetType or AttributeScope.Category)
      .WithMessage("A field is left off a type or a category (a class is the top of the structure).");
    RuleFor(x => x.TargetId).NotEmpty();
    RuleFor(x => x.DefinitionId).NotEmpty();
  }
}

public class UnhideStructureFieldCommandValidator : AbstractValidator<UnhideStructureFieldCommand>
{
  public UnhideStructureFieldCommandValidator()
  {
    RuleFor(x => x.AssignmentId).NotEmpty();
  }
}
