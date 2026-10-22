using FluentValidation;

public sealed record DisposalDecisionResult(bool IsSuccess);

/// An approver (ASSET_FINANCE.APPROVE) turns the proposal down, saying why. The asset stays as it was; the proposal
/// stays on record as rejected.
public sealed record RejectDisposalCommand(Guid AssetId, string Reason) : ICommand<Result<DisposalDecisionResult>>;

public class RejectDisposalCommandValidator : AbstractValidator<RejectDisposalCommand>
{
  public RejectDisposalCommandValidator()
  {
    RuleFor(x => x.AssetId).NotEmpty();
    RuleFor(x => x.Reason).NotEmpty().WithMessage("Say why the proposal is rejected.")
      .MinimumLength(5).WithMessage("Say why the proposal is rejected - a few words at least.").MaximumLength(500);
  }
}
