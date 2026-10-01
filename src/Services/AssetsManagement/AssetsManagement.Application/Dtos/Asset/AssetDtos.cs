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
  Guid? CustodianId,
  Guid? CurrentLocationId,
  string? Barcode,
  IReadOnlyDictionary<string, JsonElement> ExtraAttributes,
  DateTime? AttributesValidatedAt,
  bool IsActive,
  DateTime? CreatedAt,
  DateTime? LastModified);

/// Lightweight row for grids: the base columns plus only the attributes flagged is_visible_in_list.
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
  Guid? CustodianId,
  Guid? CurrentLocationId,
  string? Barcode,
  IReadOnlyDictionary<string, JsonElement> ListAttributes,
  bool IsActive,
  DateTime? CreatedAt,
  DateOnly? AcquisitionDate,
  decimal? AcquisitionCost,
  string? CurrencyCode,
  bool IsDisposed,
  DateOnly? DisposalDate);

public sealed record AssetAcquisitionDto(
  Guid Id,
  Guid AssetId,
  DateOnly AcquisitionDate,
  decimal AcquisitionCost,
  string CurrencyCode,
  decimal? ExchangeRate,
  decimal? BaseCurrencyCost,
  Guid? SupplierId,
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
  string? Title);

public sealed record AssetAssignmentDto(
  Guid Id,
  Guid AssetId,
  Guid? FromDepartmentId,
  Guid? ToDepartmentId,
  Guid? FromCustodianId,
  Guid? ToCustodianId,
  Guid? FromLocationId,
  Guid? ToLocationId,
  DateTime AssignmentDate,
  DateOnly? ExpectedReturnDate,
  DateOnly? ActualReturnDate,
  string? Reason,
  Guid? ApprovedBy,
  DateTime? ApprovedAt);

public sealed record AssetLifecycleEventDto(
  Guid Id,
  Guid AssetId,
  Guid EventTypeId,
  DateTime EventDate,
  Guid? FromStatusId,
  Guid? ToStatusId,
  Guid? PerformedBy,
  string? Notes,
  JsonElement? Details);

public static class AssetMappings
{
  public static AssetDto ToDto(this Asset x) => new(
    x.Id.Value, x.AssetCode.Value, x.Name.Value, x.Description, x.Ownership,
    x.AssetClassId.Value, x.AssetTypeId.Value, x.CategoryId.Value, x.StatusId.Value,
    x.DepartmentId, x.CustodianId, x.CurrentLocationId?.Value,
    x.Barcode, x.ExtraAttributes, x.AttributesValidatedAt,
    x.IsActive, x.CreatedAt, x.LastModified);

  public static AssetListItemDto ToListItemDto(this Asset x, IReadOnlySet<string> listAttributeCodes, AssetAcquisition? acquisition, AssetDisposal? disposal) => new(
    x.Id.Value, x.AssetCode.Value, x.Name.Value, x.Ownership,
    x.AssetClassId.Value, x.AssetTypeId.Value, x.CategoryId.Value, x.StatusId.Value,
    x.DepartmentId, x.CustodianId, x.CurrentLocationId?.Value, x.Barcode,
    x.ExtraAttributes.Where(kv => listAttributeCodes.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value),
    x.IsActive, x.CreatedAt,
    acquisition?.AcquisitionDate, acquisition?.AcquisitionCost, acquisition?.CurrencyCode.Value,
    disposal is not null, disposal?.DisposalDate);

  public static AssetAcquisitionDto ToDto(this AssetAcquisition x) => new(
    x.Id.Value, x.AssetId.Value, x.AcquisitionDate, x.AcquisitionCost, x.CurrencyCode.Value,
    x.ExchangeRate, x.BaseCurrencyCost, x.SupplierId, x.PurchaseReference, x.AcquisitionType,
    x.WarrantyStartDate, x.WarrantyExpiryDate);

  public static AssetAttachmentDto ToDto(this AssetAttachment x) => new(
    x.Id.Value, x.AssetId.Value, x.AttachmentType, x.OriginalFileName, x.StoredFileName, x.MimeType,
    x.FileSize, x.StoragePath, x.ChecksumSha256, x.IsPrimaryImage, x.CreatedAt, x.Title);

  public static AssetAssignmentDto ToDto(this AssetAssignment x) => new(
    x.Id.Value, x.AssetId.Value, x.FromDepartmentId, x.ToDepartmentId, x.FromCustodianId, x.ToCustodianId,
    x.FromLocationId?.Value, x.ToLocationId?.Value, x.AssignmentDate, x.ExpectedReturnDate, x.ActualReturnDate,
    x.Reason, x.ApprovedBy, x.ApprovedAt);

  public static AssetLifecycleEventDto ToDto(this AssetLifecycleEvent x) => new(
    x.Id.Value, x.AssetId.Value, x.EventTypeId.Value, x.EventDate, x.FromStatusId?.Value, x.ToStatusId?.Value,
    x.PerformedBy, x.Notes, x.Details);
}
