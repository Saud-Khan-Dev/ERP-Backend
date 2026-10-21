/// Entity → DTO. Master references are resolved through a MasterRefs lookup loaded once per request.
public static class Mappings
{
  public static MasterRef ToRef(this MasterData x) => new(x.Id.Value, x.Code.Value, x.Name.Value);
  public static PropertyRef ToRef(this Property x) => new(x.Id.Value, x.PropertyCode.Value, x.PropertyName.Value);
  public static OwnerRef ToRef(this PropertyOwner x) => new(x.Id.Value, x.OwnerCode.Value, x.OwnerName.Value);

  /// IsSystem: a value the application relies on (it can be renamed, never switched off or deleted); UsageCount: how many
  /// records refer to it (a value nobody refers to can be deleted).
  public static MasterDto ToDto(this MasterData x, int usageCount = 0) => new(
    x.Id.Value, x.Code.Value, x.Name.Value, x.Description, x.SortOrder, x.IsActive,
    (x as MeasurementUnit)?.FactorToBase,
    (x as MeasurementUnit)?.IsBase,
    (x as DocumentType)?.StorageFolder,
    (x as TransferType)?.RequiresRelationship,
    (x as DocumentType)?.AppliesTo,
    SystemMasterCodes.IsRequired(x.GetType(), x.Code.Value),
    usageCount);

  public static CodeSequenceDto ToDto(this CodeSequence x) => new(
    x.Key.Value, x.Prefix, x.Separator, x.MinimumDigits, x.NextNumber, x.Pattern, x.NextCode.Value);

  public static PropertyListItemDto ToListItemDto(this Property x, MasterRefs refs, decimal? totalAreaSqFt, OwnerSummaryDto? owners, string? allotteeName) => new(
    x.Id.Value, x.PropertyCode.Value, x.PropertyName.Value,
    refs[x.TownId], refs[x.PropertyTypeId], refs[x.PropertyStatusId], refs[x.PropertyClassificationId],
    x.AddressLine, x.KhasraSurveyNo, totalAreaSqFt, owners?.HolderCount ?? 0, owners, owners is null ? allotteeName : null,
    x.IsActive, x.CreatedAt);

  public static PropertyDto ToDto(this Property x, MasterRefs refs, AreaSummaryDto area, IReadOnlyList<OwnershipDto> currentOwners) => new(
    x.Id.Value, x.PropertyCode.Value, x.PropertyName.Value,
    refs[x.TownId], refs[x.PropertyTypeId], refs[x.PropertyStatusId], refs[x.PropertyClassificationId],
    x.AddressLine, x.KhasraSurveyNo, x.Description, x.Remarks, x.IsActive, area, currentOwners,
    x.CreatedAt, x.CreatedBy, x.UpdatedAt, x.UpdatedBy, x.DeactivatedOn, x.DeactivationReason);

  public static StatusHistoryDto ToDto(this PropertyStatusHistory x, MasterRefs refs) => new(
    x.Id.Value, refs[x.PropertyStatusId], x.EffectiveFrom, x.EffectiveTo, x.Reason, x.ReferenceNo, x.CreatedAt, x.CreatedBy);

  public static MeasurementDto ToDto(this PropertyMeasurement x, MasterRefs refs) => new(
    x.Id.Value, refs[x.MeasurementUnitId], x.TotalArea, x.BuiltUpArea, x.TotalAreaBase, x.BuiltUpAreaBase,
    x.MeasuredOn, x.MeasurementSource, x.IsCurrent, x.Remarks, x.CreatedAt, x.CreatedBy);

  public static RegularizationDto ToDto(this PropertyAreaRegularization x, MasterRefs refs, DateOnly today) => new(
    x.Id.Value, x.AdditionalArea, refs[x.MeasurementUnitId], x.AdditionalAreaBase, x.RegularizationStatus,
    x.ApplicationDate, x.RegularizationDate, x.OrderReferenceNo, x.ApprovedBy, x.EffectiveFrom, x.EffectiveTo,
    x.CountsTowardArea(today), x.Remarks, x.IsActive, x.ApprovedByEmployeeId);

  public static OwnerListItemDto ToListItemDto(this PropertyOwner x, MasterRefs refs, int currentHoldingCount) => new(
    x.Id.Value, x.OwnerCode.Value, refs[x.OwnerTypeId], x.OwnerName.Value, x.FatherHusbandName, x.Cnic?.Value, x.Ntn,
    x.Contacts.FirstOrDefault(c => c.IsPrimary)?.ContactNumber, currentHoldingCount, x.IsActive);

  public static OwnerDto ToDto(this PropertyOwner x, MasterRefs refs) => new(
    x.Id.Value, x.OwnerCode.Value, refs[x.OwnerTypeId], x.OwnerName.Value, x.FatherHusbandName, x.Cnic?.Value, x.Ntn,
    x.Email?.Value, x.CnicDocumentId?.Value, x.Remarks, x.IsActive,
    x.Contacts.OrderByDescending(c => c.IsActive).ThenByDescending(c => c.IsPrimary)
      .Select(c => new OwnerContactDto(c.Id.Value, refs[c.ContactTypeId], c.ContactNumber, c.IsPrimary, c.IsActive, c.Remarks)).ToList(),
    x.Addresses.OrderByDescending(a => a.IsActive).ThenByDescending(a => a.IsPrimary)
      .Select(a => new OwnerAddressDto(a.Id.Value, a.AddressType, a.FullAddress, a.CityTown, a.District, a.Province, a.Country, a.PostalCode, a.IsPrimary, a.IsActive)).ToList(),
    x.CreatedAt, x.CreatedBy);

  /// Links: what the read side worked out for the row (holding since, transfer and allotment codes).
  public static OwnershipDto ToDto(
      this PropertyOwnership x, MasterRefs refs, IReadOnlyDictionary<PropertyId, PropertyRef> properties,
      IReadOnlyDictionary<OwnerId, OwnerRef> owners, OwnershipLinks links) => new(
    x.Id.Value, properties.GetValueOrDefault(x.PropertyId), owners.GetValueOrDefault(x.OwnerId), refs[x.TenureTypeId],
    x.OwnershipSharePct, x.EffectiveFrom, x.EffectiveTo, x.OwnershipStatus,
    x.AcquisitionTransferTypeId is null ? null : refs[x.AcquisitionTransferTypeId],
    x.AcquiredViaTransferId?.Value, x.ReferenceNo, x.Remarks,
    links.HoldingSince, links.TransferNo, x.AcquiredViaAllotmentId?.Value, links.AllotmentNo, x.EndReferenceNo);

  public static TransferDto ToDto(this PropertyTransfer x, MasterRefs refs, PropertyRef? property, IReadOnlyDictionary<OwnerId, OwnerRef> owners) => new(
    x.Id.Value, property, x.TransferNo.Value, refs[x.TransferTypeId], x.TransferDate, x.TransferReferenceNo,
    x.ShareTransferredPct, x.ConsiderationAmount, x.Relationship, x.ApprovedBy, x.ApprovalDate, x.TransferStatus, x.Remarks,
    x.Parties.OrderBy(p => p.PartyRole).Select(p => new TransferPartyDto(owners.GetValueOrDefault(p.OwnerId), p.PartyRole, p.SharePct)).ToList(),
    x.CreatedAt, x.CreatedBy,
    x.ApprovalOrderRef, x.CompletedOn, x.CompletedBy, x.ClosedOn, x.ClosedBy, x.ClosingReason,
    x.ApprovedByUserId, x.ApprovedByEmployeeId, x.NoDuesCertificateNo);

  public static EncumbranceDto ToDto(this PropertyEncumbrance x, MasterRefs refs, IReadOnlyDictionary<OwnerId, OwnerRef> owners) => new(
    x.Id.Value, refs[x.EncumbranceTypeId], x.OwnershipId?.Value, x.HolderName,
    x.HolderOwnerId is null ? null : owners.GetValueOrDefault(x.HolderOwnerId),
    x.ReferenceNo, x.Amount, x.StartDate, x.EndDate, x.ReleaseDate, x.ReleaseReferenceNo, x.Status, x.Remarks);

  public static DocumentDto ToDto(this PropertyDocument x, MasterRefs refs, bool isLatest) => new(
    x.Id.Value, x.PropertyId?.Value, refs[x.DocumentTypeId], x.EntityType, x.EntityId, x.Title, x.OriginalFileName,
    x.MimeType, x.FileSizeBytes, x.ChecksumSha256, x.DocumentDate, x.ReferenceNo, x.VersionNo,
    x.SupersedesDocumentId?.Value, isLatest, x.Description, x.IsConfidential, x.IsActive, x.UploadedAt, x.UploadedBy);

  /// valueCount: how many properties hold a value for it; groupIsActive: its group is switched on.
  public static AttributeDefinitionDto ToDto(this AttributeDefinition x, MasterRefs refs, int valueCount = 0, bool groupIsActive = true) => new(
    x.Id.Value, refs[x.AttributeGroupId], x.Code.Value, x.Label.Value, x.DataType, x.IsRequired, x.Options,
    x.DefaultValue, x.DisplayOrder, x.IsActive, x.AppliesToPropertyTypeIds, x.AppliesToClassificationIds, valueCount, groupIsActive);
}
