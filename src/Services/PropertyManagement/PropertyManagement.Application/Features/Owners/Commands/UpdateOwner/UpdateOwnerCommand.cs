using FluentValidation;

public sealed record UpdateOwnerCommandResult(bool IsSuccess);

public sealed record UpdateOwnerCommand(Guid Id, OwnerInput Owner) : ICommand<Result<UpdateOwnerCommandResult>>;

public class UpdateOwnerCommandValidator : AbstractValidator<UpdateOwnerCommand>
{
  public UpdateOwnerCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Owner).NotNull().SetValidator(new OwnerInputValidator());
  }
}
