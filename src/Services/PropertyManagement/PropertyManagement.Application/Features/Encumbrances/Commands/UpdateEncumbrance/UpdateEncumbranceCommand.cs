using FluentValidation;

public sealed record UpdateEncumbranceCommandResult(bool IsSuccess);

public sealed record UpdateEncumbranceCommand(Guid Id, EncumbranceInput Encumbrance) : ICommand<Result<UpdateEncumbranceCommandResult>>;

public class UpdateEncumbranceCommandValidator : AbstractValidator<UpdateEncumbranceCommand>
{
  public UpdateEncumbranceCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Encumbrance).NotNull().SetValidator(new EncumbranceInputValidator());
  }
}
