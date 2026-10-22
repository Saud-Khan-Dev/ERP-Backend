using FluentValidation;

public sealed record CancelDisposalCommandResult(bool IsSuccess);

/// An approved disposal recorded by mistake (the wrong asset, the wrong date): the disposal is marked cancelled (it
/// stays on record), the asset goes back to the status it had before, a schedule the disposal stopped runs again, and
/// "Disposal cancelled" with the reason goes on its timeline. Months posted by the disposal stay posted (they are real
/// depreciation). ASSET_FINANCE.APPROVE.
public sealed record CancelDisposalCommand(Guid AssetId, string Reason) : ICommand<Result<CancelDisposalCommandResult>>;

public class CancelDisposalCommandValidator : AbstractValidator<CancelDisposalCommand>
{
  public CancelDisposalCommandValidator()
  {
    RuleFor(x => x.AssetId).NotEmpty();
    RuleFor(x => x.Reason).NotEmpty().WithMessage("Say why the disposal is cancelled.").MinimumLength(5).WithMessage("Say why the disposal is cancelled - a few words at least.").MaximumLength(500);
  }
}
