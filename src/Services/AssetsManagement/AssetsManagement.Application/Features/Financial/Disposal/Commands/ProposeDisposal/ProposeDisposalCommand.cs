using FluentValidation;

public sealed record ProposeDisposalCommandResult(Guid DisposalId);

/// Step one of a disposal (ASSET_FINANCE.CREATE): how the asset is to leave, on which day, for what proceeds and in
/// which final status. Nothing happens to the asset yet - another person approves it (step two). Refused while the
/// asset is out on loan, while another proposal waits, and for a date with depreciation posted after it.
public sealed record ProposeDisposalCommand(
  Guid AssetId,
  Guid DisposalMethodId,
  DateOnly DisposalDate,
  Guid ToStatusId,
  decimal? DisposalValue = null,
  string? CurrencyCode = null,
  string? BuyerInfo = null,
  string? Reason = null) : ICommand<Result<ProposeDisposalCommandResult>>;

public class ProposeDisposalCommandValidator : AbstractValidator<ProposeDisposalCommand>
{
  public ProposeDisposalCommandValidator()
  {
    RuleFor(x => x.AssetId).NotEmpty();
    RuleFor(x => x.DisposalMethodId).NotEmpty().WithMessage("Choose how the asset is to leave.");
    RuleFor(x => x.ToStatusId).NotEmpty().WithMessage("Choose the final status.");
    RuleFor(x => x.DisposalValue).GreaterThanOrEqualTo(0).When(x => x.DisposalValue.HasValue).WithMessage("The proceeds cannot be negative.");
    RuleFor(x => x.CurrencyCode).Length(3).When(x => x.CurrencyCode is not null);
    RuleFor(x => x.BuyerInfo).MaximumLength(500);
    RuleFor(x => x.Reason).MaximumLength(1000);
  }
}
