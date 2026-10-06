using FluentValidation;

// The HR catalogues: org unit types, locations, designations, document types, recruitment methods, service event
// types, request types and the BPS grades. Values are deactivated, never deleted, so old records keep their labels.

public sealed record SetCatalogActivationCommand(CatalogKind Kind, Guid Id, bool IsActive) : ICommand<Result<UpdatedResult>>;

public enum CatalogKind
{
  OrganizationUnitType,
  Location,
  Designation,
  DocumentType,
  RecruitmentMethod,
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
