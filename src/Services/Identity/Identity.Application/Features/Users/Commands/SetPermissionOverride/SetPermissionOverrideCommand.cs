using FluentValidation;

public sealed record SetPermissionOverrideCommandResult(bool IsSuccess);

/// Grants one extra permission to a single user, or withholds one their role would otherwise give.
/// DENY always wins over a role grant.
public sealed record SetPermissionOverrideCommand(
  Guid UserId,
  Guid PermissionId,
  OverrideEffect Effect,
  DateTime? ExpiresAt = null,
  string? Reason = null) : ICommand<Result<SetPermissionOverrideCommandResult>>;

public class SetPermissionOverrideCommandValidator : AbstractValidator<SetPermissionOverrideCommand>
{
  public SetPermissionOverrideCommandValidator()
  {
    RuleFor(x => x.UserId).NotEmpty();
    RuleFor(x => x.PermissionId).NotEmpty();
    RuleFor(x => x.Effect).IsInEnum();
    RuleFor(x => x.Reason).MaximumLength(500);
    RuleFor(x => x.ExpiresAt)
      .GreaterThan(DateTime.UtcNow)
      .When(x => x.ExpiresAt.HasValue)
      .WithMessage("The expiry date must be in the future.");
  }
}
