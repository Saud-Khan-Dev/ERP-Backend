using FluentValidation;

public sealed record DeleteOptionSetCommandResult(bool IsSuccess);

public sealed record DeleteOptionSetCommand(Guid Id) : ICommand<Result<DeleteOptionSetCommandResult>>;

public class DeleteOptionSetCommandValidator : AbstractValidator<DeleteOptionSetCommand>
{
  public DeleteOptionSetCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
  }
}
