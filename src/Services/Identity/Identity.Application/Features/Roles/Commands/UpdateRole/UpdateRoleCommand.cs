using FluentValidation;

public sealed record UpdateRoleCommandResult(bool IsSuccess);

public sealed record UpdateRoleCommand(Guid Id, string Code, string Name, string? Description, bool IsActive)
  : ICommand<Result<UpdateRoleCommandResult>>;

public class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
  public UpdateRoleCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Code).NotEmpty().MaximumLength(LookupCode.MaxLength);
    RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    RuleFor(x => x.Description).MaximumLength(1000);
  }
}
