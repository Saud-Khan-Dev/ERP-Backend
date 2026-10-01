using System.Text.Json;

/// One asset in a report: the register facts plus its money - purchase, depreciation written off up to the report
/// date, book value, latest valuation on or before that date, and the disposal if there was one.
public sealed record AssetReportRowDto(
  Guid Id,
  string AssetCode,
  string Name,
  string? Description,
  OwnershipType Ownership,
  Guid AssetClassId,
  Guid AssetTypeId,
  Guid CategoryId,
  Guid StatusId,
  Guid? DepartmentId,
  Guid? CustodianId,
  Guid? CurrentLocationId,
  string? Barcode,
  bool IsActive,
  DateTime? CreatedAt,
  IReadOnlyDictionary<string, JsonElement> ExtraAttributes,
  DateOnly? AcquisitionDate,
  AcquisitionType? AcquisitionType,
  decimal? AcquisitionCost,
  string? CurrencyCode,
  string? PurchaseReference,
  Guid? SupplierId,
  DateOnly? WarrantyExpiryDate,
  Guid? DepreciationMethodId,
  int? UsefulLifeMonths,
  DateOnly? DepreciationStartDate,
  decimal AccumulatedDepreciation,
  decimal? BookValue,
  decimal? LatestValuation,
  DateOnly? LatestValuationDate,
  string? LatestValuationCurrency,
  string? LatestValuationMethod,
  int Valuations,
  DateOnly? DisposalDate,
  Guid? DisposalMethodId,
  decimal? DisposalValue,
  decimal? NetBookValueAtDisposal,
  decimal? GainLoss);

/// Sums of one currency. Purchase figures go to the purchase currency, valuations to the valuation currency.
public sealed record AssetReportTotalDto(
  string CurrencyCode,
  int Assets,
  decimal Cost,
  decimal AccumulatedDepreciation,
  decimal BookValue,
  int Valued,
  decimal LatestValuation,
  int Disposed,
  decimal DisposalProceeds);

public sealed record GetAssetReportQueryResult(DateOnly AsOf, int Count, IReadOnlyList<AssetReportRowDto> Rows, IReadOnlyList<AssetReportTotalDto> Totals);

/// The assets a report covers: either the ones picked by id, or every asset matching the register's filters.
/// AsOf (default today) fixes the date depreciation and valuations are read up to. TotalsOnly answers the sums
/// without the rows (an overview figure) and is not bound by the row limit.
public sealed record GetAssetReportQuery(GetAssetsQuery Filter, IReadOnlyList<Guid>? Ids = null, DateOnly? AsOf = null, bool TotalsOnly = false)
  : IQuery<Result<GetAssetReportQueryResult>>;
