public sealed record PlacementDto(
  Guid AssignmentId,
  Guid PostId,
  string PostCode,
  Guid DesignationId,
  string Designation,
  Guid GradeId,
  int Bps,
  Guid OrgUnitId,
  string OrgUnit,
  DateOnly Since);

public sealed record EmployeeListItemDto(
  Guid Id,
  string EmployeeNumber,
  string FullName,
  string Cnic,
  Gender? Gender,
  DateOnly? DateOfBirth,
  EmploymentType EmploymentType,
  EmploymentMethod? EmploymentMethod,
  EmploymentStatus EmploymentStatus,
  RecordStatus ProfileStatus,
  Guid? UserId,
  PlacementDto? Placement,
  DateOnly? SuperannuationDate,
  string? Mobile,
  DateTime? CreatedAt);

public sealed record EmployeeContactDto(Guid Id, ContactType ContactType, string Value, bool IsPrimary);

public sealed record EmployeeAddressDto(Guid Id, AddressType AddressType, string? AddressLine, string? City, string? District, string? Province, string? PostalCode);

public sealed record EmergencyContactDto(Guid Id, string Name, string? Relation, string? Phone);

public sealed record FamilyMemberDto(Guid Id, string? Relation, string? Name, DateOnly? DateOfBirth, string? Cnic, string? Occupation);

/// The pay the employee draws now: stage and basic pay, with the scale and grade they sit in.
public sealed record CurrentPayDto(Guid PayRecordId, int Bps, string? ScaleLabel, int StageNumber, decimal BasicPay, DateOnly Since, DateOnly? NextIncrementDate);

public sealed record EmployeeDto(
  Guid Id,
  string EmployeeNumber,
  string FirstName,
  string? MiddleName,
  string? LastName,
  string FullName,
  string Cnic,
  DateOnly? DateOfBirth,
  Gender? Gender,
  string? Nationality,
  MaritalStatus? MaritalStatus,
  string? BloodGroup,
  EmploymentType EmploymentType,
  EmploymentMethod? EmploymentMethod,
  Guid? ProjectId,
  EmploymentStatus EmploymentStatus,
  RecordStatus ProfileStatus,
  Guid? UserId,
  bool HasPhoto,
  DateOnly? SuperannuationDate,
  DateOnly? ServiceStartDate,
  PlacementDto? Placement,
  CurrentPayDto? CurrentPay,
  IReadOnlyList<EmployeeContactDto> Contacts,
  IReadOnlyList<EmployeeAddressDto> Addresses,
  IReadOnlyList<EmergencyContactDto> EmergencyContacts,
  IReadOnlyList<FamilyMemberDto> FamilyMembers,
  DateTime? CreatedAt,
  DateTime? UpdatedAt);

public sealed record PayRecordDto(
  Guid Id,
  Guid PayScaleStageId,
  Guid PayScaleVersionId,
  string? ScaleLabel,
  int Bps,
  int StageNumber,
  decimal BasicPay,
  DateOnly? LastIncrementDate,
  DateOnly? NextIncrementDate,
  string? Reason,
  string? OrderNumber,
  DateOnly EffectiveFrom,
  DateOnly? EffectiveTo,
  DateTime? CreatedAt);

public sealed record AssignmentDto(
  Guid Id,
  Guid EmployeeId,
  Guid PostId,
  string PostCode,
  string? Designation,
  int Bps,
  string? OrgUnit,
  AssignmentType AssignmentType,
  DateOnly EffectiveFrom,
  DateOnly? EffectiveTo,
  RecordStatus Status,
  string? OrderNumber,
  Guid? OrderDocumentId,
  Guid? ServiceHistoryId,
  string? Remarks);

public sealed record ServiceHistoryDto(
  Guid Id,
  Guid EmployeeId,
  Guid EventTypeId,
  string? EventType,
  string? Category,
  DateOnly EffectiveDate,
  Guid? OldPostId,
  string? OldPostCode,
  Guid? NewPostId,
  string? NewPostCode,
  int? OldBps,
  int? NewBps,
  Guid? OldOrgUnitId,
  string? OldOrgUnit,
  Guid? NewOrgUnitId,
  string? NewOrgUnit,
  Guid? RecruitmentMethodId,
  string? RecruitmentMethod,
  string? ExternalReferenceOrg,
  string? OrderNumber,
  Guid? SupportingDocumentId,
  string? Reason,
  string? Remarks,
  Guid? ApprovedBy,
  DateTime? CreatedAt);

public sealed record EmployeeDocumentDto(
  Guid Id,
  Guid EmployeeId,
  Guid DocumentTypeId,
  string? DocumentType,
  string? DocumentNumber,
  DateOnly? IssueDate,
  DateOnly? ExpiryDate,
  bool IsExpired,
  VerificationStatus VerificationStatus,
  Guid? VerifiedBy,
  DateOnly? VerificationDate,
  DateTime? CreatedAt);

public sealed record EducationDto(
  Guid Id,
  Guid EmployeeId,
  QualificationLevel QualificationLevel,
  string DegreeTitle,
  string InstitutionName,
  string? BoardOrUniversity,
  int? PassingYear,
  string? GradeOrCgpa,
  bool IsHighestQualification,
  bool HasFile,
  DateTime? CreatedAt);

public static class EmployeeMappings
{
  public static PlacementDto ToDto(this Placement p) =>
      new(p.AssignmentId, p.PostId, p.PostCode, p.DesignationId, p.Designation, p.GradeId, p.Bps, p.OrgUnitId, p.OrgUnit, p.Since);

  public static EmployeeContactDto ToDto(this EmployeeContact x) => new(x.Id.Value, x.ContactType, x.Value, x.IsPrimary);

  public static EmployeeAddressDto ToDto(this EmployeeAddress x) => new(x.Id.Value, x.AddressType, x.AddressLine, x.City, x.District, x.Province, x.PostalCode);

  public static EmergencyContactDto ToDto(this EmployeeEmergencyContact x) => new(x.Id.Value, x.Name, x.Relation, x.Phone);

  public static FamilyMemberDto ToDto(this EmployeeFamilyMember x) => new(x.Id.Value, x.Relation, x.Name, x.DateOfBirth, x.Cnic, x.Occupation);

  public static EmployeeDocumentDto ToDto(this EmployeeDocument x, string? typeName, DateOnly today) => new(
    x.Id.Value, x.EmployeeId.Value, x.DocumentTypeId.Value, typeName, x.DocumentNumber, x.IssueDate, x.ExpiryDate, x.IsExpiredOn(today),
    x.VerificationStatus, x.VerifiedBy, x.VerificationDate, x.CreatedAt);

  public static EducationDto ToDto(this EmployeeEducation x) => new(
    x.Id.Value, x.EmployeeId.Value, x.QualificationLevel, x.DegreeTitle, x.InstitutionName, x.BoardOrUniversity, x.PassingYear,
    x.GradeOrCgpa, x.IsHighestQualification, x.FileReference is not null, x.CreatedAt);
}
