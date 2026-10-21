// =====================================================
// DTOs — what the API returns. Master values are always returned as {id, code, name} so a client never
// has to resolve ids itself (and never compares on an id — schema guide, rule 9).
// =====================================================

public sealed record MasterRef(Guid Id, string Code, string Name);
public sealed record PropertyRef(Guid Id, string PropertyCode, string PropertyName);
public sealed record OwnerRef(Guid Id, string OwnerCode, string OwnerName);

// ---- master data & numbering ----

/// ReadOnly: only the system's acts set these values - no new value, nothing switched off; renaming is allowed.
public sealed record MasterTypeDto(string Type, string Table, string Label, IReadOnlyList<string> ExtraFields, bool ReadOnly = false);

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
  bool? RequiresRelationship,
  DocumentScope? AppliesTo,
  bool IsSystem = false,
  int UsageCount = 0);

public sealed record CodeSequenceDto(
  string Key,
  string Prefix,
  string Separator,
  int MinimumDigits,
  long NextNumber,
  string Pattern,
  string NextCode);

// ---- property core ----

/// The holders of a property in one line: the first holder (largest share, then name) and how many there are,
/// so a register row can read "Ali Khan + 2".
public sealed record OwnerSummaryDto(string FirstHolderName, int HolderCount);

/// TotalAreaSqFt: the current measurement's total area in square feet (null when never measured).
/// CurrentOwnerCount: owners holding a current share (ACTIVE or DISPUTED). OwnerSummary: null when nobody holds a
/// share. AllotteeName: when nobody holds a share but an allotment is in force, the allottee ("Allotted to ...").
/// CreatedAt: when the property was registered.
public sealed record PropertyListItemDto(
  Guid Id,
  string PropertyCode,
  string PropertyName,
  MasterRef? Town,
  MasterRef? PropertyType,
  MasterRef? Status,
  MasterRef? Classification,
  string? AddressLine,
  string? KhasraSurveyNo,
  decimal? TotalAreaSqFt,
  int CurrentOwnerCount,
  OwnerSummaryDto? OwnerSummary,
  string? AllotteeName,
  bool IsActive,
  DateTime? CreatedAt);

/// DeactivatedOn / DeactivationReason: why and when an inactive property left the register.
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
  Guid? UpdatedBy,
  DateOnly? DeactivatedOn = null,
  string? DeactivationReason = null);

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
  bool IsActive,
  Guid? ApprovedByEmployeeId = null);

/// All areas in square feet (the base unit). EncroachedAreaSqFt: unresolved encroachments.
public sealed record AreaSummaryDto(
  Guid? CurrentMeasurementId,
  decimal? TotalAreaSqFt,
  decimal? BuiltUpAreaSqFt,
  decimal RegularizedAreaSqFt,
  decimal? TotalWithRegularizedSqFt,
  decimal EncroachedAreaSqFt);

// ---- owners ----

/// CurrentHoldingCount: the owner's current shares (ACTIVE or DISPUTED rows) on active properties.
public sealed record OwnerListItemDto(
  Guid Id,
  string OwnerCode,
  MasterRef? OwnerType,
  string OwnerName,
  string? FatherHusbandName,
  string? Cnic,
  string? Ntn,
  string? PrimaryContact,
  int CurrentHoldingCount,
  bool IsActive);

/// CnicDocumentId: the owner's current CNIC copy (latest active, latest-version CNIC_COPY owner document).
public sealed record OwnerDto(
  Guid Id,
  string OwnerCode,
  MasterRef? OwnerType,
  string OwnerName,
  string? FatherHusbandName,
  string? Cnic,
  string? Ntn,
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

/// Status: ACTIVE and DISPUTED are both held (a disputed share counts toward the 100% but cannot be
/// transferred). HoldingSince: the start of the owner's continuous holding of this property — earlier rows of the
/// same owner that ended exactly where the next began (a share changed by a transfer, a remainder after a
/// partial sale) — so it can be earlier than EffectiveFrom. AcquiredViaTransferNo / AcquiredViaAllotmentNo: the
/// codes of the transfer / allotment the row came from (a remainder keeps its original row's).
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
  string? Remarks,
  DateOnly HoldingSince,
  string? AcquiredViaTransferNo,
  Guid? AcquiredViaAllotmentId,
  string? AcquiredViaAllotmentNo,
  string? EndReferenceNo = null);

/// Read-side facts about an ownership row that need other rows: see OwnershipDto.
public sealed record OwnershipLinks(DateOnly HoldingSince, string? TransferNo, string? AllotmentNo);

public sealed record TransferPartyDto(OwnerRef? Owner, TransferPartyRole Role, decimal SharePct);

/// Status: INITIATED (waiting for approval), COMPLETED (approved and registered), REJECTED, CANCELLED, or APPROVED
/// for a row approved before approval and completion became one step (complete it).
/// CompletedOn / CompletedBy: when and by whom the ownership register was changed. ClosedOn / ClosedBy /
/// ClosingReason: the rejection or cancellation. ApprovedBy: the approver's name as it stood (the signed-in officer);
/// ApprovedByUserId / ApprovedByEmployeeId: who that is (Identity account, HRM record when linked). NoDuesCertificateNo:
/// the Taxation clearance the approval relied on.
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
  Guid? CreatedBy,
  string? ApprovalOrderRef,
  DateOnly? CompletedOn,
  string? CompletedBy,
  DateOnly? ClosedOn,
  string? ClosedBy,
  string? ClosingReason,
  Guid? ApprovedByUserId = null,
  Guid? ApprovedByEmployeeId = null,
  string? NoDuesCertificateNo = null);

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

/// PropertyId: null for an owner's own paper (EntityType Owner, EntityId = the owner's id).
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
  bool IsActive,
  IReadOnlyList<Guid>? AppliesToPropertyTypeIds = null,
  IReadOnlyList<Guid>? AppliesToClassificationIds = null,
  int ValueCount = 0,
  bool GroupIsActive = true);

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
  string? Value,
  bool IsHidden = false);
