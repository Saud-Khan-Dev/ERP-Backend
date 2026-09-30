using FluentValidation;

public sealed record EndOwnershipCommandResult(bool IsSuccess);

/// Closes an ownership period without a transfer (e.g. surrender to GDA). The row stays as history.
public sealed record EndOwnershipCommand(Guid Id, DateOnly EffectiveTo, string? Remarks = null) : ICommand<Result<EndOwnershipCommandResult>>;

public class EndOwnershipCommandValidator : AbstractValidator<EndOwnershipCommand>
{
  public EndOwnershipCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Remarks).MaximumLength(4000);
  }
}
