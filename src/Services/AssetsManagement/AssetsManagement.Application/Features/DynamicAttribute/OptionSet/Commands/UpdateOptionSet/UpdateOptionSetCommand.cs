using FluentValidation;

public sealed record UpdateOptionSetCommandResult(bool IsSuccess);

/// Updates the header only; values are managed through the /values endpoints.
public sealed record UpdateOptionSetCommand(Guid Id, string Code, string Name, string? Description, bool IsActive)
  : ICommand<Result<UpdateOptionSetCommandResult>>;

public class UpdateOptionSetCommandValidator : AbstractValidator<UpdateOptionSetCommand>
{
  public UpdateOptionSetCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
    RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
  }
}
