using FluentValidation;

public sealed record RemovePermissionOverrideCommandResult(bool IsSuccess);

/// Drops the override so the user falls back to whatever their roles grant.
public sealed record RemovePermissionOverrideCommand(Guid UserId, Guid PermissionId)
  : ICommand<Result<RemovePermissionOverrideCommandResult>>;

public class RemovePermissionOverrideCommandValidator : AbstractValidator<RemovePermissionOverrideCommand>
{
  public RemovePermissionOverrideCommandValidator()
  {
    RuleFor(x => x.UserId).NotEmpty();
    RuleFor(x => x.PermissionId).NotEmpty();
  }
}
