using FluentValidation;

public sealed record UpdateDepreciationMethodCommandResult(bool IsSuccess);

public sealed record UpdateDepreciationMethodCommand(Guid Id, DepreciationMethodInput Method) : ICommand<Result<UpdateDepreciationMethodCommandResult>>;

public class UpdateDepreciationMethodCommandValidator : AbstractValidator<UpdateDepreciationMethodCommand>
{
  public UpdateDepreciationMethodCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Method).NotNull().SetValidator(new DepreciationMethodInputValidator());
  }
}
