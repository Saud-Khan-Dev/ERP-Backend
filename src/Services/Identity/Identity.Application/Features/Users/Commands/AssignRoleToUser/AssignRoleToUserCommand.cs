using FluentValidation;

public sealed record AssignRoleToUserCommandResult(Guid UserRoleId);

/// Grants a role, optionally until a date — for covering leave without leaving the access behind.
public sealed record AssignRoleToUserCommand(Guid UserId, Guid RoleId, DateTime? ExpiresAt = null)
  : ICommand<Result<AssignRoleToUserCommandResult>>;

public class AssignRoleToUserCommandValidator : AbstractValidator<AssignRoleToUserCommand>
{
  public AssignRoleToUserCommandValidator()
  {
    RuleFor(x => x.UserId).NotEmpty();
    RuleFor(x => x.RoleId).NotEmpty();
    RuleFor(x => x.ExpiresAt)
      .GreaterThan(DateTime.UtcNow)
      .When(x => x.ExpiresAt.HasValue)
      .WithMessage("The expiry date must be in the future.");
  }
}
