using FluentValidation;

public sealed record AssetAcquisitionInput(
  DateOnly AcquisitionDate,
  decimal AcquisitionCost,
  string CurrencyCode,
  AcquisitionType AcquisitionType,
  decimal? ExchangeRate = null,
  Guid? SupplierId = null,
  string? PurchaseReference = null,
  DateOnly? WarrantyStartDate = null,
  DateOnly? WarrantyExpiryDate = null);

public sealed record UpsertAssetAcquisitionCommandResult(Guid Id, bool Created);

/// One acquisition per asset: creates it on first call, updates it afterwards.
public sealed record UpsertAssetAcquisitionCommand(Guid AssetId, AssetAcquisitionInput Acquisition) : ICommand<Result<UpsertAssetAcquisitionCommandResult>>;

public class AssetAcquisitionInputValidator : AbstractValidator<AssetAcquisitionInput>
{
  public AssetAcquisitionInputValidator()
  {
    RuleFor(x => x.AcquisitionCost).GreaterThanOrEqualTo(0);
    RuleFor(x => x.CurrencyCode).NotEmpty().Length(3);
    RuleFor(x => x.AcquisitionType).IsInEnum();
    RuleFor(x => x.ExchangeRate).GreaterThan(0).When(x => x.ExchangeRate.HasValue);
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
