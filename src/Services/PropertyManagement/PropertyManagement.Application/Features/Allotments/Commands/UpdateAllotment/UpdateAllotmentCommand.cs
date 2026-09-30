using FluentValidation;

public sealed record UpdateAllotmentCommandResult(bool IsSuccess);

public sealed record UpdateAllotmentCommand(Guid Id, AllotmentDetailsInput Allotment) : ICommand<Result<UpdateAllotmentCommandResult>>;

public class UpdateAllotmentCommandValidator : AbstractValidator<UpdateAllotmentCommand>
{
  public UpdateAllotmentCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Allotment).NotNull().SetValidator(new AllotmentDetailsInputValidator());
  }
}
