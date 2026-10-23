using FluentValidation;

public sealed record ApproveDisposalCommandResult(Guid DisposalId, decimal? NetBookValueAtDisposal, decimal? GainLoss, int MonthsPosted);

/// Step two (ASSET_FINANCE.APPROVE): the proposal waiting on the asset is approved by another person than the one who
/// proposed it (the rule "Disposals pass through two people", unless a Super Admin switched it off). In one
/// transaction: the months up to the disposal date are written and posted, the book value on that date is kept, the
/// schedule stops, the asset moves to the proposed final status and "Disposed" goes on its timeline. Everything is
/// checked again - the asset may have been lent out, a method switched off or a month posted since the proposal.
public sealed record ApproveDisposalCommand(Guid AssetId, string? Note = null) : ICommand<Result<ApproveDisposalCommandResult>>;

public class ApproveDisposalCommandValidator : AbstractValidator<ApproveDisposalCommand>
{
  public ApproveDisposalCommandValidator()
  {
    RuleFor(x => x.AssetId).NotEmpty();
    RuleFor(x => x.Note).MaximumLength(500);
  }
}
