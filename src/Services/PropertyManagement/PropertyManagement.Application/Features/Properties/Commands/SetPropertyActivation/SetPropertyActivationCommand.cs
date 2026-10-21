using FluentValidation;

public sealed record SetPropertyActivationCommandResult(bool IsActive);

/// Nothing is hard-deleted: a property is deactivated (with the reason) and keeps its whole history.
public sealed record SetPropertyActivationCommand(Guid Id, bool IsActive, string? Reason = null) : ICommand<Result<SetPropertyActivationCommandResult>>;

public class SetPropertyActivationCommandValidator : AbstractValidator<SetPropertyActivationCommand>
{
  public SetPropertyActivationCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Reason).NotEmpty().When(x => !x.IsActive)
      .WithMessage("Give the reason the property leaves the register (entered twice, merged into another, acquired ...).");
    RuleFor(x => x.Reason).MaximumLength(4000);
  }
}
