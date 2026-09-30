using FluentValidation;

public sealed record CreateMasterCommandResult(Guid Id);

public sealed record CreateMasterCommand(string Type, MasterInput Item) : ICommand<Result<CreateMasterCommandResult>>;

public class CreateMasterCommandValidator : AbstractValidator<CreateMasterCommand>
{
  public CreateMasterCommandValidator()
  {
    RuleFor(x => x.Type).NotEmpty();
    RuleFor(x => x.Item).NotNull().SetValidator(new MasterInputValidator());
  }
}
