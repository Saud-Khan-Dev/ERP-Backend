public sealed record OrganizationUnitTypeDto(Guid Id, string Name, string Code, int? HierarchyLevel, bool IsActive);

public sealed record LocationDto(Guid Id, string Name, string? AddressLine, string? City, string? District, string? Province, bool IsActive);

public sealed record DesignationDto(Guid Id, string Title, string? Code, string? Description, bool IsActive);

public sealed record PayScaleGradeDto(Guid Id, int BpsNumber, string? GradeName, string Label, bool IsActive);

public sealed record DocumentTypeDto(Guid Id, string Name, bool RequiresExpiry, bool IsActive);

public sealed record RecruitmentMethodDto(Guid Id, string Name, bool IsActive);

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

  public static ServiceEventTypeDto ToDto(this ServiceEventType x) => new(x.Id.Value, x.Name, x.Category, ServiceEventNames.IsSystem(x.Name), x.IsActive);

  public static EmployeeRequestTypeDto ToDto(this EmployeeRequestType x) => new(x.Id.Value, x.Name, x.Code, x.RequiresDocument, x.IsActive);
}
