using FluentValidation;

/// Warnings: what the change means elsewhere (a new town changes the tax file's number and circle in Taxation).
public sealed record UpdatePropertyCommandResult(bool IsSuccess, IReadOnlyList<string> Warnings);

public sealed record UpdatePropertyCommand(Guid Id, PropertyInput Property) : ICommand<Result<UpdatePropertyCommandResult>>;

public class UpdatePropertyCommandValidator : AbstractValidator<UpdatePropertyCommand>
{
  public UpdatePropertyCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Property).NotNull().SetValidator(new PropertyInputValidator());
  }
}
