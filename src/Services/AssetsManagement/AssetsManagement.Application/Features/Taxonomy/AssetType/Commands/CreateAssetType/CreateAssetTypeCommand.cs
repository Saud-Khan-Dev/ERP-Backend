using FluentValidation;

public sealed record AssetTypeInput(
  Guid AssetClassId,
  string Code,
  string Name,
  string? Description,
  bool IsDepreciable = true,
  bool RequiresLocation = true,
  bool RequiresCustodian = true,
  int? DisplayOrder = null,
  bool IsActive = true);

public sealed record CreateAssetTypeCommandResult(Guid Id);

public sealed record CreateAssetTypeCommand(AssetTypeInput AssetType) : ICommand<Result<CreateAssetTypeCommandResult>>;

public class AssetTypeInputValidator : AbstractValidator<AssetTypeInput>
{
  public AssetTypeInputValidator()
  {
    RuleFor(x => x.AssetClassId).NotEmpty();
    RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
    RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
  }
}

public class CreateAssetTypeCommandValidator : AbstractValidator<CreateAssetTypeCommand>
{
  public CreateAssetTypeCommandValidator()
  {
    RuleFor(x => x.AssetType).NotNull().SetValidator(new AssetTypeInputValidator());
  }
}
