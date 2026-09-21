using FluentValidation;

public sealed record CreateAssetValuationCommandResult(Guid Id);

public sealed record CreateAssetValuationCommand(
  Guid AssetId,
  DateOnly ValuationDate,
  decimal Value,
  string CurrencyCode,
  string? ValuationMethod = null,
  Guid? ValuedBy = null,
  string? Notes = null) : ICommand<Result<CreateAssetValuationCommandResult>>;

public class CreateAssetValuationCommandValidator : AbstractValidator<CreateAssetValuationCommand>
{
  public CreateAssetValuationCommandValidator()
  {
    RuleFor(x => x.AssetId).NotEmpty();
    RuleFor(x => x.Value).GreaterThanOrEqualTo(0);
    RuleFor(x => x.CurrencyCode).NotEmpty().Length(3);
    RuleFor(x => x.ValuationMethod).MaximumLength(100);
  }
}
