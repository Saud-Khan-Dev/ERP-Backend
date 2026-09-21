using FluentValidation;

public sealed record UpdateAssetCategoryCommandResult(bool IsSuccess);

/// Also moves the category when ParentCategoryId changes; the whole subtree's paths are rebuilt.
public sealed record UpdateAssetCategoryCommand(Guid Id, AssetCategoryInput Category) : ICommand<Result<UpdateAssetCategoryCommandResult>>;

public class UpdateAssetCategoryCommandValidator : AbstractValidator<UpdateAssetCategoryCommand>
{
  public UpdateAssetCategoryCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Category).NotNull().SetValidator(new AssetCategoryInputValidator());
  }
}
