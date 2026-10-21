using FluentValidation;

public sealed record AssetAcquisitionInput(
  DateOnly AcquisitionDate,
  decimal AcquisitionCost,
  string CurrencyCode,
  AcquisitionType AcquisitionType,
  decimal? ExchangeRate = null,
  string? SupplierName = null,
  string? PurchaseReference = null,
  DateOnly? WarrantyStartDate = null,
  DateOnly? WarrantyExpiryDate = null);

public sealed record UpsertAssetAcquisitionCommandResult(Guid Id, bool Created);

/// One acquisition per asset: creates it on first call (ASSET_FINANCE.CREATE), updates it afterwards
/// (ASSET_FINANCE.EDIT). Once depreciation is posted on it, its cost, date and currency stay as they are.
public sealed record UpsertAssetAcquisitionCommand(Guid AssetId, AssetAcquisitionInput Acquisition) : ICommand<Result<UpsertAssetAcquisitionCommandResult>>;

public class AssetAcquisitionInputValidator : AbstractValidator<AssetAcquisitionInput>
{
  public AssetAcquisitionInputValidator()
  {
    RuleFor(x => x.AcquisitionCost).GreaterThanOrEqualTo(0).WithMessage("The cost cannot be negative.");
    RuleFor(x => x.CurrencyCode).NotEmpty().Length(3).WithMessage("Choose the currency.");
    RuleFor(x => x.AcquisitionType).IsInEnum();
    RuleFor(x => x.ExchangeRate).GreaterThan(0).When(x => x.ExchangeRate.HasValue).WithMessage("The exchange rate must be greater than zero.");
    RuleFor(x => x.SupplierName).MaximumLength(200);
    RuleFor(x => x.PurchaseReference).MaximumLength(100);
  }
}

public class UpsertAssetAcquisitionCommandValidator : AbstractValidator<UpsertAssetAcquisitionCommand>
{
  public UpsertAssetAcquisitionCommandValidator()
  {
    RuleFor(x => x.AssetId).NotEmpty();
    RuleFor(x => x.Acquisition).NotNull().SetValidator(new AssetAcquisitionInputValidator());
  }
}
