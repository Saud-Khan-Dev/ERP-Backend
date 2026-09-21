using FluentValidation;

public sealed record DeleteAssetCategoryCommandResult(bool IsSuccess);

public sealed record DeleteAssetCategoryCommand(Guid Id) : ICommand<Result<DeleteAssetCategoryCommandResult>>;

public class DeleteAssetCategoryCommandValidator : AbstractValidator<DeleteAssetCategoryCommand>
{
  public DeleteAssetCategoryCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
  }
}
