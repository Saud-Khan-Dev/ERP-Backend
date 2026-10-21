using FluentValidation;

public sealed record EndOwnershipCommandResult(bool IsSuccess);

/// Closes an ownership period without a transfer (e.g. surrender to GDA, an order cancelling the entry): an approver's
/// act. Nobody takes the share over, so the order or reference it ends under is required. The row stays as history.
public sealed record EndOwnershipCommand(Guid Id, DateOnly EffectiveTo, string? EndReferenceNo, string? Remarks = null) : ICommand<Result<EndOwnershipCommandResult>>;

public class EndOwnershipCommandValidator : AbstractValidator<EndOwnershipCommand>
{
  public EndOwnershipCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.EndReferenceNo).NotEmpty()
      .WithMessage("Give the order or reference under which the share ends.")
      .MaximumLength(PropertyOwnership.ReferenceMaxLength);
    RuleFor(x => x.Remarks).MaximumLength(4000);
  }
}
