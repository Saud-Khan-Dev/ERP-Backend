using FluentValidation;

/// The person who proposed the disposal (ASSET_FINANCE.CREATE) takes it back while it waits. Anyone else asks an
/// approver to reject it.
public sealed record WithdrawDisposalCommand(Guid AssetId, string? Reason = null) : ICommand<Result<DisposalDecisionResult>>;

public class WithdrawDisposalCommandValidator : AbstractValidator<WithdrawDisposalCommand>
{
  public WithdrawDisposalCommandValidator()
  {
    RuleFor(x => x.AssetId).NotEmpty();
    RuleFor(x => x.Reason).MaximumLength(500);
  }
}
