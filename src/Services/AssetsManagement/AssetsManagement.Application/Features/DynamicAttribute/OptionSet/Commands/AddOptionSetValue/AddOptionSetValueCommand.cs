using FluentValidation;

public sealed record AddOptionSetValueCommandResult(Guid Id);

public sealed record AddOptionSetValueCommand(Guid OptionSetId, OptionSetValueInput Value) : ICommand<Result<AddOptionSetValueCommandResult>>;

public class AddOptionSetValueCommandValidator : AbstractValidator<AddOptionSetValueCommand>
{
  public AddOptionSetValueCommandValidator()
  {
    RuleFor(x => x.OptionSetId).NotEmpty();
    RuleFor(x => x.Value).NotNull().SetValidator(new OptionSetValueInputValidator());
  }
}
