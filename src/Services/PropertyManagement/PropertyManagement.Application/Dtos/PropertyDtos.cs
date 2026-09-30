// =====================================================
// DTOs — what the API returns. Master values are always returned as {id, code, name} so a client never
// has to resolve ids itself (and never compares on an id — schema guide, rule 9).
// =====================================================

public sealed record MasterRef(Guid Id, string Code, string Name);
public sealed record PropertyRef(Guid Id, string PropertyCode, string PropertyName);
public sealed record OwnerRef(Guid Id, string OwnerCode, string OwnerName);

// ---- master data & numbering ----

public sealed record MasterTypeDto(string Type, string Table, string Label, IReadOnlyList<string> ExtraFields);

public sealed record MasterDto(
  Guid Id,
  string Code,
  string Name,
  string? Description,
  int SortOrder,
  bool IsActive,
  decimal? FactorToBase,
  bool? IsBase,
  string? StorageFolder,
  bool? RequiresRelationship);

public sealed record CodeSequenceDto(
  string Key,
  string Prefix,
  string Separator,
  int MinimumDigits,
  long NextNumber,
  string Pattern,
  string NextCode);

// ---- property core ----

public sealed record PropertyListItemDto(
  Guid Id,
  string PropertyCode,
  string PropertyName,
  MasterRef? Town,
  MasterRef? PropertyType,
  MasterRef? Status,
  MasterRef? Classification,
  string? AddressLine,
  bool IsActive);

public sealed record PropertyDto(
  Guid Id,
  string PropertyCode,
  string PropertyName,
  MasterRef? Town,
  MasterRef? PropertyType,
  MasterRef? Status,
  MasterRef? Classification,
  string? AddressLine,
  string? KhasraSurveyNo,
  string? Description,
  string? Remarks,
  bool IsActive,
  AreaSummaryDto Area,
  IReadOnlyList<OwnershipDto> CurrentOwners,
  DateTime? CreatedAt,
  Guid? CreatedBy,
  DateTime? UpdatedAt,
  Guid? UpdatedBy);

public sealed record StatusHistoryDto(
  Guid Id,
  MasterRef? Status,
  DateOnly EffectiveFrom,
  DateOnly? EffectiveTo,
  string? Reason,
  string? ReferenceNo,
  DateTime? CreatedAt,
  Guid? CreatedBy);

public sealed record MeasurementDto(
  Guid Id,
  MasterRef? Unit,
  decimal TotalArea,
  decimal BuiltUpArea,
  decimal TotalAreaBase,
  decimal BuiltUpAreaBase,
  DateOnly? MeasuredOn,
  string? MeasurementSource,
  bool IsCurrent,
  string? Remarks,
  DateTime? CreatedAt,
  Guid? CreatedBy);

public sealed record RegularizationDto(
  Guid Id,
  decimal AdditionalArea,
  MasterRef? Unit,
  decimal AdditionalAreaBase,
  RegularizationStatus Status,
  DateOnly? ApplicationDate,
  DateOnly? RegularizationDate,
  string? OrderReferenceNo,
  string? ApprovedBy,
  DateOnly? EffectiveFrom,
  DateOnly? EffectiveTo,
  bool CountsTowardArea,
  string? Remarks,
  bool IsActive);

/// All areas in square feet (the base unit). EncroachedAreaSqFt: unresolved encroachments.
public sealed record AreaSummaryDto(
  Guid? CurrentMeasurementId,
  decimal? TotalAreaSqFt,
  decimal? BuiltUpAreaSqFt,
  decimal RegularizedAreaSqFt,
  decimal? TotalWithRegularizedSqFt,
  decimal EncroachedAreaSqFt);

// ---- owners ----

public sealed record OwnerListItemDto(
  Guid Id,
  string OwnerCode,
  MasterRef? OwnerType,
  string OwnerName,
  string? FatherHusbandName,
  string? Cnic,
  string? Ntn,
  string? PrimaryContact,
  bool IsActive);

public sealed record OwnerDto(
  Guid Id,
  string OwnerCode,
  MasterRef? OwnerType,
  string OwnerName,
  string? FatherHusbandName,
  string? Cnic,
  string? Ntn,
  string? RegistrationNo,
  string? Email,
  Guid? CnicDocumentId,
  string? Remarks,
  bool IsActive,
  IReadOnlyList<OwnerContactDto> Contacts,
  IReadOnlyList<OwnerAddressDto> Addresses,
  DateTime? CreatedAt,
  Guid? CreatedBy);

public sealed record OwnerContactDto(Guid Id, MasterRef? ContactType, string ContactNumber, bool IsPrimary, bool IsActive, string? Remarks);

public sealed record OwnerAddressDto(
  Guid Id,
  AddressType AddressType,
  string FullAddress,
  string? CityTown,
  string? District,
  string? Province,
  string Country,
  string? PostalCode,
  bool IsPrimary,
  bool IsActive);

// ---- ownership ----

public sealed record OwnershipDto(
  Guid Id,
  PropertyRef? Property,
  OwnerRef? Owner,
  MasterRef? TenureType,
  decimal OwnershipSharePct,
  DateOnly EffectiveFrom,
  DateOnly? EffectiveTo,
  OwnershipStatus Status,
  MasterRef? AcquisitionTransferType,
  Guid? AcquiredViaTransferId,
  string? ReferenceNo,
  string? Remarks);

public sealed record TransferPartyDto(OwnerRef? Owner, TransferPartyRole Role, decimal SharePct);

public sealed record TransferDto(
  Guid Id,
  PropertyRef? Property,
  string TransferNo,
  MasterRef? TransferType,
  DateOnly TransferDate,
  string? TransferReferenceNo,
  decimal ShareTransferredPct,
  decimal? ConsiderationAmount,
  string? Relationship,
  string? ApprovedBy,
  DateOnly? ApprovalDate,
  TransferStatus Status,
  string? Remarks,
  IReadOnlyList<TransferPartyDto> Parties,
  DateTime? CreatedAt,
  Guid? CreatedBy);

public sealed record EncumbranceDto(
  Guid Id,
  MasterRef? EncumbranceType,
  Guid? OwnershipId,
  string HolderName,
  OwnerRef? HolderOwner,
  string? ReferenceNo,
  decimal? Amount,
  DateOnly StartDate,
  DateOnly? EndDate,
  DateOnly? ReleaseDate,
  string? ReleaseReferenceNo,
  EncumbranceStatus Status,
  string? Remarks);

// ---- documents ----

public sealed record DocumentDto(
  Guid Id,
  Guid? PropertyId,
  MasterRef? DocumentType,
  DocumentEntityType EntityType,
  Guid EntityId,
  string? Title,
  string OriginalFileName,
  string MimeType,
  long FileSizeBytes,
  string ChecksumSha256,
  DateOnly? DocumentDate,
  string? ReferenceNo,
  int VersionNo,
  Guid? SupersedesDocumentId,
  bool IsLatest,
  string? Description,
  bool IsConfidential,
  bool IsActive,
  DateTime UploadedAt,
  Guid UploadedBy);

// ---- custom fields ----

public sealed record AttributeDefinitionDto(
  Guid Id,
  MasterRef? Group,
  string Code,
  string Label,
  AttributeDataType DataType,
  bool IsRequired,
  IReadOnlyList<string> Options,
  string? DefaultValue,
  int DisplayOrder,
  bool IsActive);

public sealed record PropertyAttributeValueDto(
  Guid? Id,
  Guid AttributeDefinitionId,
  MasterRef? Group,
  string Code,
  string Label,
  AttributeDataType DataType,
  bool IsRequired,
  IReadOnlyList<string> Options,
  int DisplayOrder,
  string? Value);
