using FluentValidation;

public sealed record DeleteAttributeAssignmentCommandResult(bool IsSuccess);

public sealed record DeleteAttributeAssignmentCommand(Guid Id) : ICommand<Result<DeleteAttributeAssignmentCommandResult>>;

public class DeleteAttributeAssignmentCommandValidator : AbstractValidator<DeleteAttributeAssignmentCommand>
{
  public DeleteAttributeAssignmentCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
  }
}
