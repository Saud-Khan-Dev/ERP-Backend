using FluentValidation;

public sealed record CreateAssetValuationCommandResult(Guid Id);

/// What the asset was found to be worth on a date - kept as its value history (the book value does not change).
public sealed record CreateAssetValuationCommand(
  Guid AssetId,
  DateOnly ValuationDate,
  decimal Value,
  string CurrencyCode,
  string? ValuationMethod = null,
  string? ValuedByName = null,
  string? Notes = null) : ICommand<Result<CreateAssetValuationCommandResult>>;

public class CreateAssetValuationCommandValidator : AbstractValidator<CreateAssetValuationCommand>
{
  public CreateAssetValuationCommandValidator()
  {
    RuleFor(x => x.AssetId).NotEmpty();
    RuleFor(x => x.Value).GreaterThanOrEqualTo(0).WithMessage("The value cannot be negative.");
    RuleFor(x => x.CurrencyCode).NotEmpty().Length(3).WithMessage("Choose the currency.");
    RuleFor(x => x.ValuationMethod).MaximumLength(100);
    RuleFor(x => x.ValuedByName).MaximumLength(150);
    RuleFor(x => x.Notes).MaximumLength(2000);
  }
}
