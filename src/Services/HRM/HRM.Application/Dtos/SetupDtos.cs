public sealed record OrganizationUnitTypeDto(Guid Id, string Name, string Code, int? HierarchyLevel, bool IsActive);

public sealed record LocationDto(Guid Id, string Name, string? AddressLine, string? City, string? District, string? Province, bool IsActive);

public sealed record DesignationDto(Guid Id, string Title, string? Code, string? Description, bool IsActive);

public sealed record PayScaleGradeDto(Guid Id, int BpsNumber, string? GradeName, string Label, bool IsActive);

public sealed record DocumentTypeDto(Guid Id, string Name, bool RequiresExpiry, bool IsActive);

public sealed record RecruitmentMethodDto(Guid Id, string Name, bool IsActive);

public sealed record QualificationLevelDto(Guid Id, string Name, int Rank, bool IsActive);

public sealed record FamilyRelationDto(Guid Id, string Name, bool IsActive);

public sealed record EmploymentMethodDto(Guid Id, string Name, string? Description, bool IsActive);

/// IsSystem: written by the arrears run (Arrears, Recovery) - never switched off, its direction fixed.
public sealed record AdjustmentTypeDto(Guid Id, string Name, AdjustmentDirection Direction, bool IsSystem, bool IsActive);

/// Basis: the rules the type follows. InUse: employees, posts or salary rules use it, so the basis is fixed.
public sealed record EmploymentTypeDto(Guid Id, string Name, string? Description, EmploymentBasis Basis, bool InUse, bool IsActive);

/// IsSystem: HR actions and separations record under this type; it cannot be renamed or deactivated.
public sealed record ServiceEventTypeDto(Guid Id, string Name, string? Category, bool IsSystem, bool IsActive);

public sealed record EmployeeRequestTypeDto(Guid Id, string Name, string? Code, bool RequiresDocument, bool IsActive);

public static class SetupMappings
{
  public static OrganizationUnitTypeDto ToDto(this OrganizationUnitType x) => new(x.Id.Value, x.Name, x.Code, x.HierarchyLevel, x.IsActive);

  public static LocationDto ToDto(this Location x) => new(x.Id.Value, x.Name, x.AddressLine, x.City, x.District, x.Province, x.IsActive);

  public static DesignationDto ToDto(this Designation x) => new(x.Id.Value, x.Title, x.Code, x.Description, x.IsActive);

  public static PayScaleGradeDto ToDto(this PayScaleGrade x) => new(x.Id.Value, x.BpsNumber, x.GradeName, x.Label, x.IsActive);

  public static DocumentTypeDto ToDto(this DocumentType x) => new(x.Id.Value, x.Name, x.RequiresExpiry, x.IsActive);

  public static RecruitmentMethodDto ToDto(this RecruitmentMethod x) => new(x.Id.Value, x.Name, x.IsActive);

  public static QualificationLevelDto ToDto(this QualificationLevel x) => new(x.Id.Value, x.Name, x.Rank, x.IsActive);

  public static FamilyRelationDto ToDto(this FamilyRelation x) => new(x.Id.Value, x.Name, x.IsActive);

  public static EmploymentMethodDto ToDto(this EmploymentMethod x) => new(x.Id.Value, x.Name, x.Description, x.IsActive);

  public static AdjustmentTypeDto ToDto(this AdjustmentType x) => new(x.Id.Value, x.Name, x.Direction, x.IsSystem, x.IsActive);

  public static EmploymentTypeDto ToDto(this EmploymentType x, bool inUse) => new(x.Id.Value, x.Name, x.Description, x.Basis, inUse, x.IsActive);

  public static ServiceEventTypeDto ToDto(this ServiceEventType x) => new(x.Id.Value, x.Name, x.Category, ServiceEventNames.IsSystem(x.Name), x.IsActive);

  public static EmployeeRequestTypeDto ToDto(this EmployeeRequestType x) => new(x.Id.Value, x.Name, x.Code, x.RequiresDocument, x.IsActive);
}
