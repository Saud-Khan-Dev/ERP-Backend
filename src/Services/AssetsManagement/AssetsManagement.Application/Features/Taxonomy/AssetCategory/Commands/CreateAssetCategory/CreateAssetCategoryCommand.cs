using FluentValidation;

public sealed record AssetCategoryInput(
  Guid AssetClassId,
  Guid AssetTypeId,
  Guid? ParentCategoryId,
  string Code,
  string Name,
  string? Description,
  int? DisplayOrder = null,
  bool IsActive = true);

public sealed record CreateAssetCategoryCommandResult(Guid Id);

public sealed record CreateAssetCategoryCommand(AssetCategoryInput Category) : ICommand<Result<CreateAssetCategoryCommandResult>>;

public class AssetCategoryInputValidator : AbstractValidator<AssetCategoryInput>
{
  public AssetCategoryInputValidator()
  {
    RuleFor(x => x.AssetClassId).NotEmpty();
    RuleFor(x => x.AssetTypeId).NotEmpty().WithMessage("Choose the asset type the category belongs to.");
    RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
    RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
  }
}

public class CreateAssetCategoryCommandValidator : AbstractValidator<CreateAssetCategoryCommand>
{
  public CreateAssetCategoryCommandValidator()
  {
    RuleFor(x => x.Category).NotNull().SetValidator(new AssetCategoryInputValidator());
  }
}
