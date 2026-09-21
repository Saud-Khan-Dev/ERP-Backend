using FluentValidation;

public sealed record DeleteDepreciationMethodCommandResult(bool IsSuccess);

public sealed record DeleteDepreciationMethodCommand(Guid Id) : ICommand<Result<DeleteDepreciationMethodCommandResult>>;

public class DeleteDepreciationMethodCommandValidator : AbstractValidator<DeleteDepreciationMethodCommand>
{
  public DeleteDepreciationMethodCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
  }
}
