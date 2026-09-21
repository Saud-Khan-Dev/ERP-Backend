using FluentValidation;

public sealed record DisposeAssetCommandResult(Guid DisposalId);

/// Records the disposal, snapshots the net book value, closes the active depreciation schedule,
/// moves the asset to the given terminal status and writes the lifecycle event.
public sealed record DisposeAssetCommand(
  Guid AssetId,
  Guid DisposalMethodId,
  DateOnly DisposalDate,
  Guid ToStatusId,
  Guid EventTypeId,
  decimal? DisposalValue = null,
  string? CurrencyCode = null,
  string? BuyerInfo = null,
  string? Reason = null,
  Guid? ApprovedBy = null,
  DateTime? ApprovedAt = null,
  Guid? PerformedBy = null) : ICommand<Result<DisposeAssetCommandResult>>;

public class DisposeAssetCommandValidator : AbstractValidator<DisposeAssetCommand>
{
  public DisposeAssetCommandValidator()
  {
    RuleFor(x => x.AssetId).NotEmpty();
    RuleFor(x => x.DisposalMethodId).NotEmpty();
    RuleFor(x => x.ToStatusId).NotEmpty();
    RuleFor(x => x.EventTypeId).NotEmpty();
    RuleFor(x => x.DisposalValue).GreaterThanOrEqualTo(0).When(x => x.DisposalValue.HasValue);
    RuleFor(x => x.CurrencyCode).Length(3).When(x => x.CurrencyCode is not null);
    RuleFor(x => x.CurrencyCode).NotEmpty().When(x => x.DisposalValue.HasValue).WithMessage("A currency is required when a disposal value is given.");
  }
}
