using System.Text.Json;

public sealed record AssetDto(
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
  string? DepartmentName,
  Guid? CustodianId,
  string? CustodianName,
  string? CustodianNumber,
  Guid? CurrentLocationId,
  string? Barcode,
  IReadOnlyDictionary<string, JsonElement> ExtraAttributes,
  DateTime? AttributesValidatedAt,
  bool IsActive,
  DateTime? CreatedAt,
  string? CreatedBy,
  DateTime? LastModified,
  string? LastModifiedBy);

/// Lightweight row for grids: the base columns plus only the attributes flagged is_visible_in_list. The purchase facts
/// (date, cost, currency) are null for a caller without ASSET_FINANCE.VIEW.
public sealed record AssetListItemDto(
  Guid Id,
  string AssetCode,
  string Name,
  OwnershipType Ownership,
  Guid AssetClassId,
  Guid AssetTypeId,
  Guid CategoryId,
  Guid StatusId,
  Guid? DepartmentId,
  string? DepartmentName,
  Guid? CustodianId,
  string? CustodianName,
  string? CustodianNumber,
  Guid? CurrentLocationId,
  string? Barcode,
  IReadOnlyDictionary<string, JsonElement> ListAttributes,
  bool IsActive,
  DateTime? CreatedAt,
  DateOnly? AcquisitionDate,
  decimal? AcquisitionCost,
  string? CurrencyCode,
  DateOnly? WarrantyExpiryDate,
  bool IsDisposed,
  DateOnly? DisposalDate,
  /// Out on a loan that has not come back: the date it is due back.
  DateOnly? LoanDueBack,
  /// When the asset entered its current status.
  DateTime? StatusSince);

public sealed record AssetAcquisitionDto(
  Guid Id,
  Guid AssetId,
  DateOnly AcquisitionDate,
  decimal AcquisitionCost,
  string CurrencyCode,
  decimal? ExchangeRate,
  decimal? BaseCurrencyCost,
  string? SupplierName,
  string? PurchaseReference,
  AcquisitionType AcquisitionType,
  DateOnly? WarrantyStartDate,
  DateOnly? WarrantyExpiryDate);

public sealed record AssetAttachmentDto(
  Guid Id,
  Guid AssetId,
  AttachmentType AttachmentType,
  string OriginalFileName,
  string StoredFileName,
  string MimeType,
  long FileSize,
  string StoragePath,
  string? ChecksumSha256,
  bool IsPrimaryImage,
  DateTime? CreatedAt,
  string? CreatedBy,
  string? Title);

public sealed record AssetAssignmentDto(
  Guid Id,
  Guid AssetId,
  Guid? FromDepartmentId,
  string? FromDepartmentName,
  Guid? ToDepartmentId,
  string? ToDepartmentName,
  Guid? FromCustodianId,
  string? FromCustodianName,
  string? FromCustodianNumber,
  Guid? ToCustodianId,
  string? ToCustodianName,
  string? ToCustodianNumber,
  Guid? FromLocationId,
  Guid? ToLocationId,
  DateTime AssignmentDate,
  DateOnly? ExpectedReturnDate,
  DateOnly? ActualReturnDate,
  string? Reason,
  Guid? RecordedBy,
  string? RecordedByName);

public sealed record AssetLifecycleEventDto(
  Guid Id,
  Guid AssetId,
  Guid EventTypeId,
  DateTime EventDate,
  Guid? FromStatusId,
  Guid? ToStatusId,
  Guid? PerformedBy,
  string? PerformedByName,
  string? Notes,
  JsonElement? Details);

public static class AssetMappings
{
  public static AssetDto ToDto(this Asset x) => new(
    x.Id.Value, x.AssetCode.Value, x.Name.Value, x.Description, x.Ownership,
    x.AssetClassId.Value, x.AssetTypeId.Value, x.CategoryId.Value, x.StatusId.Value,
    x.DepartmentId, x.DepartmentName, x.CustodianId, x.CustodianName, x.CustodianNumber, x.CurrentLocationId?.Value,
    x.Barcode, x.ExtraAttributes, x.AttributesValidatedAt,
    x.IsActive, x.CreatedAt, x.CreatedBy, x.LastModified, x.LastModifiedBy);

  public static AssetListItemDto ToListItemDto(
      this Asset x, IReadOnlySet<string> listAttributeCodes, AssetAcquisition? acquisition, AssetDisposal? disposal,
      bool includeMoney, DateOnly? loanDueBack, DateTime? statusSince) => new(
    x.Id.Value, x.AssetCode.Value, x.Name.Value, x.Ownership,
    x.AssetClassId.Value, x.AssetTypeId.Value, x.CategoryId.Value, x.StatusId.Value,
    x.DepartmentId, x.DepartmentName, x.CustodianId, x.CustodianName, x.CustodianNumber, x.CurrentLocationId?.Value, x.Barcode,
    x.ExtraAttributes.Where(kv => listAttributeCodes.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value),
    x.IsActive, x.CreatedAt,
    includeMoney ? acquisition?.AcquisitionDate : null, includeMoney ? acquisition?.AcquisitionCost : null, includeMoney ? acquisition?.CurrencyCode.Value : null,
    acquisition?.WarrantyExpiryDate,
    disposal is not null, disposal?.DisposalDate, loanDueBack, statusSince);

  public static AssetAcquisitionDto ToDto(this AssetAcquisition x) => new(
    x.Id.Value, x.AssetId.Value, x.AcquisitionDate, x.AcquisitionCost, x.CurrencyCode.Value,
    x.ExchangeRate, x.BaseCurrencyCost, x.SupplierName, x.PurchaseReference, x.AcquisitionType,
    x.WarrantyStartDate, x.WarrantyExpiryDate);

  public static AssetAttachmentDto ToDto(this AssetAttachment x) => new(
    x.Id.Value, x.AssetId.Value, x.AttachmentType, x.OriginalFileName, x.StoredFileName, x.MimeType,
    x.FileSize, x.StoragePath, x.ChecksumSha256, x.IsPrimaryImage, x.CreatedAt, x.CreatedBy, x.Title);

  public static AssetAssignmentDto ToDto(this AssetAssignment x) => new(
    x.Id.Value, x.AssetId.Value, x.FromDepartmentId, x.FromDepartmentName, x.ToDepartmentId, x.ToDepartmentName,
    x.FromCustodianId, x.FromCustodianName, x.FromCustodianNumber, x.ToCustodianId, x.ToCustodianName, x.ToCustodianNumber,
    x.FromLocationId?.Value, x.ToLocationId?.Value, x.AssignmentDate, x.ExpectedReturnDate, x.ActualReturnDate,
    x.Reason, x.RecordedBy, x.RecordedByName);

  public static AssetLifecycleEventDto ToDto(this AssetLifecycleEvent x) => new(
    x.Id.Value, x.AssetId.Value, x.EventTypeId.Value, x.EventDate, x.FromStatusId?.Value, x.ToStatusId?.Value,
    x.PerformedBy, x.PerformedByName, x.Notes, x.Details);
}
