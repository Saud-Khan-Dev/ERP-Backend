using FluentValidation;

public sealed record AssetClassInput(string Code, string Name, string? Description, int? DisplayOrder, bool IsActive = true);

public sealed record CreateAssetClassCommandResult(Guid Id);

public sealed record CreateAssetClassCommand(AssetClassInput AssetClass) : ICommand<Result<CreateAssetClassCommandResult>>;

public class AssetClassInputValidator : AbstractValidator<AssetClassInput>
{
  public AssetClassInputValidator()
  {
    RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
    RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
  }
}

public class CreateAssetClassCommandValidator : AbstractValidator<CreateAssetClassCommand>
{
  public CreateAssetClassCommandValidator()
  {
    RuleFor(x => x.AssetClass).NotNull().SetValidator(new AssetClassInputValidator());
  }
}
