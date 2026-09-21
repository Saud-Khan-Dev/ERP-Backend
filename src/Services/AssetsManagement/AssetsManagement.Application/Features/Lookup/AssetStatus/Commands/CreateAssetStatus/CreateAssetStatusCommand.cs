using FluentValidation;

public sealed record AssetStatusInput(
  string Code,
  string Name,
  string? Description,
  bool IsTerminal = false,
  bool AllowsAssignment = true,
  string? Color = null,
  int? DisplayOrder = null,
  bool IsActive = true);

public sealed record CreateAssetStatusCommandResult(Guid Id);

public sealed record CreateAssetStatusCommand(AssetStatusInput Status) : ICommand<Result<CreateAssetStatusCommandResult>>;

public class AssetStatusInputValidator : AbstractValidator<AssetStatusInput>
{
  public AssetStatusInputValidator()
  {
    RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
    RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    RuleFor(x => x.Color).MaximumLength(20);
  }
}

public class CreateAssetStatusCommandValidator : AbstractValidator<CreateAssetStatusCommand>
{
  public CreateAssetStatusCommandValidator()
  {
    RuleFor(x => x.Status).NotNull().SetValidator(new AssetStatusInputValidator());
  }
}
