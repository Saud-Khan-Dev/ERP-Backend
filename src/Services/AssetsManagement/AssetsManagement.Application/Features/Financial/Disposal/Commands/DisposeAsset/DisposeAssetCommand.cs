using FluentValidation;

public sealed record DisposeAssetCommandResult(Guid DisposalId, decimal? NetBookValueAtDisposal, decimal? GainLoss, int MonthsPosted);

/// Records how an asset left GDA (ASSET_FINANCE.APPROVE - recording it is the approval). In one transaction: the
/// months up to the disposal date are written and posted, the book value on that date is kept, the schedule stops,
/// the asset moves to the final status, and "Disposed" goes on its timeline. Refused while it is out on loan.
public sealed record DisposeAssetCommand(
  Guid AssetId,
  Guid DisposalMethodId,
  DateOnly DisposalDate,
  Guid ToStatusId,
  decimal? DisposalValue = null,
  string? CurrencyCode = null,
  string? BuyerInfo = null,
  string? Reason = null) : ICommand<Result<DisposeAssetCommandResult>>;

public class DisposeAssetCommandValidator : AbstractValidator<DisposeAssetCommand>
{
  public DisposeAssetCommandValidator()
  {
    RuleFor(x => x.AssetId).NotEmpty();
    RuleFor(x => x.DisposalMethodId).NotEmpty().WithMessage("Choose how the asset left.");
    RuleFor(x => x.ToStatusId).NotEmpty().WithMessage("Choose the final status.");
    RuleFor(x => x.DisposalValue).GreaterThanOrEqualTo(0).When(x => x.DisposalValue.HasValue).WithMessage("The proceeds cannot be negative.");
    RuleFor(x => x.CurrencyCode).Length(3).When(x => x.CurrencyCode is not null);
    RuleFor(x => x.BuyerInfo).MaximumLength(500);
    RuleFor(x => x.Reason).MaximumLength(1000);
  }
}
