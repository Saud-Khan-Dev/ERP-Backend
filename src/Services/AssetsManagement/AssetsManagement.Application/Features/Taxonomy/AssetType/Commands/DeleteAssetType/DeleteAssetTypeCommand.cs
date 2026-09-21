using FluentValidation;

public sealed record DeleteAssetTypeCommandResult(bool IsSuccess);

public sealed record DeleteAssetTypeCommand(Guid Id) : ICommand<Result<DeleteAssetTypeCommandResult>>;

public class DeleteAssetTypeCommandValidator : AbstractValidator<DeleteAssetTypeCommand>
{
  public DeleteAssetTypeCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
  }
}
