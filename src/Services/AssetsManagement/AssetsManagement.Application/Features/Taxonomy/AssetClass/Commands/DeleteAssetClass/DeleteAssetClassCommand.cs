using FluentValidation;

public sealed record DeleteAssetClassCommandResult(bool IsSuccess);

public sealed record DeleteAssetClassCommand(Guid Id) : ICommand<Result<DeleteAssetClassCommandResult>>;

public class DeleteAssetClassCommandValidator : AbstractValidator<DeleteAssetClassCommand>
{
  public DeleteAssetClassCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
  }
}
