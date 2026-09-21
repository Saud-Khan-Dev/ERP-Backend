using FluentValidation;

public sealed record DeleteAssetStatusCommandResult(bool IsSuccess);

public sealed record DeleteAssetStatusCommand(Guid Id) : ICommand<Result<DeleteAssetStatusCommandResult>>;

public class DeleteAssetStatusCommandValidator : AbstractValidator<DeleteAssetStatusCommand>
{
  public DeleteAssetStatusCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
  }
}
