using FluentValidation;

public sealed record DeleteLocationCommandResult(bool IsSuccess);

public sealed record DeleteLocationCommand(Guid Id) : ICommand<Result<DeleteLocationCommandResult>>;

public class DeleteLocationCommandValidator : AbstractValidator<DeleteLocationCommand>
{
  public DeleteLocationCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
  }
}
