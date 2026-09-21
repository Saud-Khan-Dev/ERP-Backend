using FluentValidation;

public sealed record RemoveOptionSetValueCommandResult(bool IsSuccess);

public sealed record RemoveOptionSetValueCommand(Guid OptionSetId, Guid ValueId) : ICommand<Result<RemoveOptionSetValueCommandResult>>;

public class RemoveOptionSetValueCommandValidator : AbstractValidator<RemoveOptionSetValueCommand>
{
  public RemoveOptionSetValueCommandValidator()
  {
    RuleFor(x => x.OptionSetId).NotEmpty();
    RuleFor(x => x.ValueId).NotEmpty();
  }
}
