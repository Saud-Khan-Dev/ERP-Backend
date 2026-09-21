using FluentValidation;

public sealed record UpdateOptionSetValueCommandResult(bool IsSuccess);

public sealed record UpdateOptionSetValueCommand(Guid OptionSetId, Guid ValueId, OptionSetValueInput Value)
  : ICommand<Result<UpdateOptionSetValueCommandResult>>;

public class UpdateOptionSetValueCommandValidator : AbstractValidator<UpdateOptionSetValueCommand>
{
  public UpdateOptionSetValueCommandValidator()
  {
    RuleFor(x => x.OptionSetId).NotEmpty();
    RuleFor(x => x.ValueId).NotEmpty();
    RuleFor(x => x.Value).NotNull().SetValidator(new OptionSetValueInputValidator());
  }
}
