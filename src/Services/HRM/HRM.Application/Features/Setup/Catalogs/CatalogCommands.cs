using FluentValidation;

// The HR catalogues: org unit types, locations, designations, document types, recruitment methods, employment types,
// service event types, request types and the BPS grades. Values are deactivated, never deleted, so old records keep their labels.

public sealed record SetCatalogActivationCommand(CatalogKind Kind, Guid Id, bool IsActive) : ICommand<Result<UpdatedResult>>;

public enum CatalogKind
{
  OrganizationUnitType,
  Location,
  Designation,
  DocumentType,
  RecruitmentMethod,
  EmploymentType,
  QualificationLevel,
  FamilyRelation,
  EmploymentMethod,
  AdjustmentType,
  ServiceEventType,
  RequestType
}

public sealed record OrganizationUnitTypeInput(string Name, string Code, int? HierarchyLevel);
public sealed record CreateOrganizationUnitTypeCommand(OrganizationUnitTypeInput Type) : ICommand<Result<CreatedResult>>;
public sealed record UpdateOrganizationUnitTypeCommand(Guid Id, OrganizationUnitTypeInput Type) : ICommand<Result<UpdatedResult>>;

public sealed record LocationInput(string Name, string? AddressLine, string? City, string? District, string? Province);
public sealed record CreateLocationCommand(LocationInput Location) : ICommand<Result<CreatedResult>>;
public sealed record UpdateLocationCommand(Guid Id, LocationInput Location) : ICommand<Result<UpdatedResult>>;

public sealed record DesignationInput(string Title, string? Code, string? Description);
public sealed record CreateDesignationCommand(DesignationInput Designation) : ICommand<Result<CreatedResult>>;
public sealed record UpdateDesignationCommand(Guid Id, DesignationInput Designation) : ICommand<Result<UpdatedResult>>;

public sealed record DocumentTypeInput(string Name, bool RequiresExpiry);
public sealed record CreateDocumentTypeCommand(DocumentTypeInput Type) : ICommand<Result<CreatedResult>>;
public sealed record UpdateDocumentTypeCommand(Guid Id, DocumentTypeInput Type) : ICommand<Result<UpdatedResult>>;

public sealed record RecruitmentMethodInput(string Name);
public sealed record CreateRecruitmentMethodCommand(RecruitmentMethodInput Method) : ICommand<Result<CreatedResult>>;
public sealed record UpdateRecruitmentMethodCommand(Guid Id, RecruitmentMethodInput Method) : ICommand<Result<UpdatedResult>>;

public sealed record QualificationLevelInput(string Name, int Rank);
public sealed record CreateQualificationLevelCommand(QualificationLevelInput Level) : ICommand<Result<CreatedResult>>;
public sealed record UpdateQualificationLevelCommand(Guid Id, QualificationLevelInput Level) : ICommand<Result<UpdatedResult>>;

public sealed record FamilyRelationInput(string Name);
public sealed record CreateFamilyRelationCommand(FamilyRelationInput Relation) : ICommand<Result<CreatedResult>>;
public sealed record UpdateFamilyRelationCommand(Guid Id, FamilyRelationInput Relation) : ICommand<Result<UpdatedResult>>;

public sealed record EmploymentMethodInput(string Name, string? Description);
public sealed record CreateEmploymentMethodCommand(EmploymentMethodInput Method) : ICommand<Result<CreatedResult>>;
public sealed record UpdateEmploymentMethodCommand(Guid Id, EmploymentMethodInput Method) : ICommand<Result<UpdatedResult>>;

public sealed record AdjustmentTypeInput(string Name, AdjustmentDirection Direction);
public sealed record CreateAdjustmentTypeCommand(AdjustmentTypeInput Type) : ICommand<Result<CreatedResult>>;
public sealed record UpdateAdjustmentTypeCommand(Guid Id, AdjustmentTypeInput Type) : ICommand<Result<UpdatedResult>>;

public sealed record EmploymentTypeInput(string Name, string? Description, EmploymentBasis Basis);
public sealed record CreateEmploymentTypeCommand(EmploymentTypeInput Type) : ICommand<Result<CreatedResult>>;
public sealed record UpdateEmploymentTypeCommand(Guid Id, EmploymentTypeInput Type) : ICommand<Result<UpdatedResult>>;

public sealed record ServiceEventTypeInput(string Name, string? Category);
public sealed record CreateServiceEventTypeCommand(ServiceEventTypeInput Type) : ICommand<Result<CreatedResult>>;
public sealed record UpdateServiceEventTypeCommand(Guid Id, ServiceEventTypeInput Type) : ICommand<Result<UpdatedResult>>;

public sealed record RequestTypeInput(string Name, string? Code, bool RequiresDocument);
public sealed record CreateRequestTypeCommand(RequestTypeInput Type) : ICommand<Result<CreatedResult>>;
public sealed record UpdateRequestTypeCommand(Guid Id, RequestTypeInput Type) : ICommand<Result<UpdatedResult>>;

public sealed record UpdatePayScaleGradeCommand(Guid Id, string? GradeName, bool IsActive) : ICommand<Result<UpdatedResult>>;

public class OrganizationUnitTypeInputValidator : AbstractValidator<OrganizationUnitTypeInput>
{
  public OrganizationUnitTypeInputValidator()
  {
    RuleFor(x => x.Name).NotEmpty().MaximumLength(OrganizationUnitType.NameMaxLength);
    RuleFor(x => x.Code).NotEmpty().MaximumLength(OrganizationUnitType.CodeMaxLength);
    RuleFor(x => x.HierarchyLevel).GreaterThanOrEqualTo(0).When(x => x.HierarchyLevel.HasValue);
  }
}

public class CreateOrganizationUnitTypeCommandValidator : AbstractValidator<CreateOrganizationUnitTypeCommand>
{
  public CreateOrganizationUnitTypeCommandValidator() => RuleFor(x => x.Type).NotNull().SetValidator(new OrganizationUnitTypeInputValidator());
}

public class UpdateOrganizationUnitTypeCommandValidator : AbstractValidator<UpdateOrganizationUnitTypeCommand>
{
  public UpdateOrganizationUnitTypeCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Type).NotNull().SetValidator(new OrganizationUnitTypeInputValidator());
  }
}

public class LocationInputValidator : AbstractValidator<LocationInput>
{
  public LocationInputValidator()
  {
    RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    RuleFor(x => x.AddressLine).MaximumLength(300);
    RuleFor(x => x.City).MaximumLength(100);
    RuleFor(x => x.District).MaximumLength(100);
    RuleFor(x => x.Province).MaximumLength(100);
  }
}

public class CreateLocationCommandValidator : AbstractValidator<CreateLocationCommand>
{
  public CreateLocationCommandValidator() => RuleFor(x => x.Location).NotNull().SetValidator(new LocationInputValidator());
}

public class UpdateLocationCommandValidator : AbstractValidator<UpdateLocationCommand>
{
  public UpdateLocationCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Location).NotNull().SetValidator(new LocationInputValidator());
  }
}

public class DesignationInputValidator : AbstractValidator<DesignationInput>
{
  public DesignationInputValidator()
  {
    RuleFor(x => x.Title).NotEmpty().MaximumLength(Designation.TitleMaxLength);
    RuleFor(x => x.Code).MaximumLength(Designation.CodeMaxLength);
    RuleFor(x => x.Description).MaximumLength(2000);
  }
}

public class CreateDesignationCommandValidator : AbstractValidator<CreateDesignationCommand>
{
  public CreateDesignationCommandValidator() => RuleFor(x => x.Designation).NotNull().SetValidator(new DesignationInputValidator());
}

public class UpdateDesignationCommandValidator : AbstractValidator<UpdateDesignationCommand>
{
  public UpdateDesignationCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Designation).NotNull().SetValidator(new DesignationInputValidator());
  }
}

public class DocumentTypeInputValidator : AbstractValidator<DocumentTypeInput>
{
  public DocumentTypeInputValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
}

public class CreateDocumentTypeCommandValidator : AbstractValidator<CreateDocumentTypeCommand>
{
  public CreateDocumentTypeCommandValidator() => RuleFor(x => x.Type).NotNull().SetValidator(new DocumentTypeInputValidator());
}

public class UpdateDocumentTypeCommandValidator : AbstractValidator<UpdateDocumentTypeCommand>
{
  public UpdateDocumentTypeCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Type).NotNull().SetValidator(new DocumentTypeInputValidator());
  }
}

public class RecruitmentMethodInputValidator : AbstractValidator<RecruitmentMethodInput>
{
  public RecruitmentMethodInputValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
}

public class CreateRecruitmentMethodCommandValidator : AbstractValidator<CreateRecruitmentMethodCommand>
{
  public CreateRecruitmentMethodCommandValidator() => RuleFor(x => x.Method).NotNull().SetValidator(new RecruitmentMethodInputValidator());
}

public class UpdateRecruitmentMethodCommandValidator : AbstractValidator<UpdateRecruitmentMethodCommand>
{
  public UpdateRecruitmentMethodCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Method).NotNull().SetValidator(new RecruitmentMethodInputValidator());
  }
}

public class QualificationLevelInputValidator : AbstractValidator<QualificationLevelInput>
{
  public QualificationLevelInputValidator()
  {
    RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    RuleFor(x => x.Rank).InclusiveBetween(1, 99);
  }
}

public class CreateQualificationLevelCommandValidator : AbstractValidator<CreateQualificationLevelCommand>
{
  public CreateQualificationLevelCommandValidator() => RuleFor(x => x.Level).NotNull().SetValidator(new QualificationLevelInputValidator());
}

public class UpdateQualificationLevelCommandValidator : AbstractValidator<UpdateQualificationLevelCommand>
{
  public UpdateQualificationLevelCommandValidator() => RuleFor(x => x.Level).NotNull().SetValidator(new QualificationLevelInputValidator());
}

public class CreateFamilyRelationCommandValidator : AbstractValidator<CreateFamilyRelationCommand>
{
  public CreateFamilyRelationCommandValidator() => RuleFor(x => x.Relation.Name).NotEmpty().MaximumLength(60);
}

public class UpdateFamilyRelationCommandValidator : AbstractValidator<UpdateFamilyRelationCommand>
{
  public UpdateFamilyRelationCommandValidator() => RuleFor(x => x.Relation.Name).NotEmpty().MaximumLength(60);
}

public class EmploymentMethodInputValidator : AbstractValidator<EmploymentMethodInput>
{
  public EmploymentMethodInputValidator()
  {
    RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    RuleFor(x => x.Description).MaximumLength(200);
  }
}

public class CreateEmploymentMethodCommandValidator : AbstractValidator<CreateEmploymentMethodCommand>
{
  public CreateEmploymentMethodCommandValidator() => RuleFor(x => x.Method).NotNull().SetValidator(new EmploymentMethodInputValidator());
}

public class UpdateEmploymentMethodCommandValidator : AbstractValidator<UpdateEmploymentMethodCommand>
{
  public UpdateEmploymentMethodCommandValidator() => RuleFor(x => x.Method).NotNull().SetValidator(new EmploymentMethodInputValidator());
}

public class AdjustmentTypeInputValidator : AbstractValidator<AdjustmentTypeInput>
{
  public AdjustmentTypeInputValidator()
  {
    RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    RuleFor(x => x.Direction).IsInEnum();
  }
}

public class CreateAdjustmentTypeCommandValidator : AbstractValidator<CreateAdjustmentTypeCommand>
{
  public CreateAdjustmentTypeCommandValidator() => RuleFor(x => x.Type).NotNull().SetValidator(new AdjustmentTypeInputValidator());
}

public class UpdateAdjustmentTypeCommandValidator : AbstractValidator<UpdateAdjustmentTypeCommand>
{
  public UpdateAdjustmentTypeCommandValidator() => RuleFor(x => x.Type).NotNull().SetValidator(new AdjustmentTypeInputValidator());
}

public class EmploymentTypeInputValidator : AbstractValidator<EmploymentTypeInput>
{
  public EmploymentTypeInputValidator()
  {
    RuleFor(x => x.Name).NotEmpty().MaximumLength(EmploymentType.NameMaxLength);
    RuleFor(x => x.Description).MaximumLength(EmploymentType.DescriptionMaxLength);
    RuleFor(x => x.Basis).IsInEnum();
  }
}

public class CreateEmploymentTypeCommandValidator : AbstractValidator<CreateEmploymentTypeCommand>
{
  public CreateEmploymentTypeCommandValidator() => RuleFor(x => x.Type).NotNull().SetValidator(new EmploymentTypeInputValidator());
}

public class UpdateEmploymentTypeCommandValidator : AbstractValidator<UpdateEmploymentTypeCommand>
{
  public UpdateEmploymentTypeCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Type).NotNull().SetValidator(new EmploymentTypeInputValidator());
  }
}

public class ServiceEventTypeInputValidator : AbstractValidator<ServiceEventTypeInput>
{
  public ServiceEventTypeInputValidator()
  {
    RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    RuleFor(x => x.Category).MaximumLength(50);
  }
}

public class CreateServiceEventTypeCommandValidator : AbstractValidator<CreateServiceEventTypeCommand>
{
  public CreateServiceEventTypeCommandValidator() => RuleFor(x => x.Type).NotNull().SetValidator(new ServiceEventTypeInputValidator());
}

public class UpdateServiceEventTypeCommandValidator : AbstractValidator<UpdateServiceEventTypeCommand>
{
  public UpdateServiceEventTypeCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Type).NotNull().SetValidator(new ServiceEventTypeInputValidator());
  }
}

public class RequestTypeInputValidator : AbstractValidator<RequestTypeInput>
{
  public RequestTypeInputValidator()
  {
    RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    RuleFor(x => x.Code).MaximumLength(30);
  }
}

public class CreateRequestTypeCommandValidator : AbstractValidator<CreateRequestTypeCommand>
{
  public CreateRequestTypeCommandValidator() => RuleFor(x => x.Type).NotNull().SetValidator(new RequestTypeInputValidator());
}

public class UpdateRequestTypeCommandValidator : AbstractValidator<UpdateRequestTypeCommand>
{
  public UpdateRequestTypeCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Type).NotNull().SetValidator(new RequestTypeInputValidator());
  }
}

public class UpdatePayScaleGradeCommandValidator : AbstractValidator<UpdatePayScaleGradeCommand>
{
  public UpdatePayScaleGradeCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.GradeName).MaximumLength(50);
  }
}
