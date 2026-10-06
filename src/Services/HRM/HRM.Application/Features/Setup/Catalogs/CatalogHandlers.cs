using Microsoft.EntityFrameworkCore;

public class CreateOrganizationUnitTypeHandler(IApplicationDbContext context)
  : ICommandHandler<CreateOrganizationUnitTypeCommand, Result<CreatedResult>>
{
  public async Task<Result<CreatedResult>> Handle(CreateOrganizationUnitTypeCommand command, CancellationToken cancellationToken)
  {
    var type = OrganizationUnitType.Create(OrganizationUnitTypeId.New(), command.Type.Name, command.Type.Code, command.Type.HierarchyLevel);

    if (await context.OrganizationUnitTypes.AnyAsync(t => t.Name.ToLower() == type.Name.ToLower() || t.Code == type.Code, cancellationToken))
      return Result<CreatedResult>.Failure($"A unit type named '{type.Name}' or coded {type.Code} already exists.");

    context.OrganizationUnitTypes.Add(type);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(type.Id);
  }
}

public class UpdateOrganizationUnitTypeHandler(IApplicationDbContext context)
  : ICommandHandler<UpdateOrganizationUnitTypeCommand, Result<UpdatedResult>>
{
  public async Task<Result<UpdatedResult>> Handle(UpdateOrganizationUnitTypeCommand command, CancellationToken cancellationToken)
  {
    var type = await context.LoadUnitTypeAsync(command.Id, cancellationToken);
    type.Update(command.Type.Name, command.Type.Code, command.Type.HierarchyLevel);

    if (await context.OrganizationUnitTypes.AnyAsync(t => t.Id != type.Id && (t.Name.ToLower() == type.Name.ToLower() || t.Code == type.Code), cancellationToken))
      return Result<UpdatedResult>.Failure($"Another unit type is named '{type.Name}' or coded {type.Code}.");

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }
}

public class CreateLocationHandler(IApplicationDbContext context) : ICommandHandler<CreateLocationCommand, Result<CreatedResult>>
{
  public async Task<Result<CreatedResult>> Handle(CreateLocationCommand command, CancellationToken cancellationToken)
  {
    var input = command.Location;
    var location = Location.Create(LocationId.New(), input.Name, input.AddressLine, input.City, input.District, input.Province);

    if (await context.Locations.AnyAsync(l => l.Name.ToLower() == location.Name.ToLower(), cancellationToken))
      return Result<CreatedResult>.Failure($"A location named '{location.Name}' already exists.");

    context.Locations.Add(location);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(location.Id);
  }
}

public class UpdateLocationHandler(IApplicationDbContext context) : ICommandHandler<UpdateLocationCommand, Result<UpdatedResult>>
{
  public async Task<Result<UpdatedResult>> Handle(UpdateLocationCommand command, CancellationToken cancellationToken)
  {
    var location = await context.LoadLocationAsync(command.Id, cancellationToken);
    var input = command.Location;
    location.Update(input.Name, input.AddressLine, input.City, input.District, input.Province);

    if (await context.Locations.AnyAsync(l => l.Id != location.Id && l.Name.ToLower() == location.Name.ToLower(), cancellationToken))
      return Result<UpdatedResult>.Failure($"Another location is named '{location.Name}'.");

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }
}

public class CreateDesignationHandler(IApplicationDbContext context) : ICommandHandler<CreateDesignationCommand, Result<CreatedResult>>
{
  public async Task<Result<CreatedResult>> Handle(CreateDesignationCommand command, CancellationToken cancellationToken)
  {
    var input = command.Designation;
    var designation = Designation.Create(DesignationId.New(), input.Title, input.Code, input.Description);

    if (await context.Designations.AnyAsync(d => d.Title.ToLower() == designation.Title.ToLower() || (designation.Code != null && d.Code == designation.Code), cancellationToken))
      return Result<CreatedResult>.Failure($"A designation titled '{designation.Title}'{(designation.Code is null ? "" : $" or coded {designation.Code}")} already exists.");

    context.Designations.Add(designation);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(designation.Id);
  }
}

public class UpdateDesignationHandler(IApplicationDbContext context) : ICommandHandler<UpdateDesignationCommand, Result<UpdatedResult>>
{
  public async Task<Result<UpdatedResult>> Handle(UpdateDesignationCommand command, CancellationToken cancellationToken)
  {
    var designation = await context.LoadDesignationAsync(command.Id, cancellationToken);
    var input = command.Designation;
    designation.Update(input.Title, input.Code, input.Description);

    if (await context.Designations.AnyAsync(d => d.Id != designation.Id && (d.Title.ToLower() == designation.Title.ToLower() || (designation.Code != null && d.Code == designation.Code)), cancellationToken))
      return Result<UpdatedResult>.Failure($"Another designation has the title '{designation.Title}' or the same code.");

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }
}

public class CreateDocumentTypeHandler(IApplicationDbContext context) : ICommandHandler<CreateDocumentTypeCommand, Result<CreatedResult>>
{
  public async Task<Result<CreatedResult>> Handle(CreateDocumentTypeCommand command, CancellationToken cancellationToken)
  {
    var type = DocumentType.Create(DocumentTypeId.New(), command.Type.Name, command.Type.RequiresExpiry);

    if (await context.DocumentTypes.AnyAsync(t => t.Name.ToLower() == type.Name.ToLower(), cancellationToken))
      return Result<CreatedResult>.Failure($"A document type named '{type.Name}' already exists.");

    context.DocumentTypes.Add(type);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(type.Id);
  }
}

public class UpdateDocumentTypeHandler(IApplicationDbContext context) : ICommandHandler<UpdateDocumentTypeCommand, Result<UpdatedResult>>
{
  public async Task<Result<UpdatedResult>> Handle(UpdateDocumentTypeCommand command, CancellationToken cancellationToken)
  {
    var type = await context.LoadDocumentTypeAsync(command.Id, cancellationToken);
    type.Update(command.Type.Name, command.Type.RequiresExpiry);

    if (await context.DocumentTypes.AnyAsync(t => t.Id != type.Id && t.Name.ToLower() == type.Name.ToLower(), cancellationToken))
      return Result<UpdatedResult>.Failure($"Another document type is named '{type.Name}'.");

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }
}

public class CreateRecruitmentMethodHandler(IApplicationDbContext context) : ICommandHandler<CreateRecruitmentMethodCommand, Result<CreatedResult>>
{
  public async Task<Result<CreatedResult>> Handle(CreateRecruitmentMethodCommand command, CancellationToken cancellationToken)
  {
    var method = RecruitmentMethod.Create(RecruitmentMethodId.New(), command.Method.Name);

    if (await context.RecruitmentMethods.AnyAsync(m => m.Name.ToLower() == method.Name.ToLower(), cancellationToken))
      return Result<CreatedResult>.Failure($"A recruitment method named '{method.Name}' already exists.");

    context.RecruitmentMethods.Add(method);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(method.Id);
  }
}

public class UpdateRecruitmentMethodHandler(IApplicationDbContext context) : ICommandHandler<UpdateRecruitmentMethodCommand, Result<UpdatedResult>>
{
  public async Task<Result<UpdatedResult>> Handle(UpdateRecruitmentMethodCommand command, CancellationToken cancellationToken)
  {
    var methodId = RecruitmentMethodId.Of(command.Id);
    var method = await context.RecruitmentMethods.FirstOrDefaultAsync(m => m.Id == methodId, cancellationToken)
      ?? throw new RecruitmentMethodNotFoundException($"Recruitment method {command.Id} was not found.");
    method.Update(command.Method.Name);

    if (await context.RecruitmentMethods.AnyAsync(m => m.Id != method.Id && m.Name.ToLower() == method.Name.ToLower(), cancellationToken))
      return Result<UpdatedResult>.Failure($"Another recruitment method is named '{method.Name}'.");

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }
}

public class CreateServiceEventTypeHandler(IApplicationDbContext context) : ICommandHandler<CreateServiceEventTypeCommand, Result<CreatedResult>>
{
  public async Task<Result<CreatedResult>> Handle(CreateServiceEventTypeCommand command, CancellationToken cancellationToken)
  {
    var type = ServiceEventType.Create(ServiceEventTypeId.New(), command.Type.Name, command.Type.Category);

    if (await context.ServiceEventTypes.AnyAsync(t => t.Name.ToLower() == type.Name.ToLower(), cancellationToken))
      return Result<CreatedResult>.Failure($"A service event type named '{type.Name}' already exists.");

    context.ServiceEventTypes.Add(type);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(type.Id);
  }
}

public class UpdateServiceEventTypeHandler(IApplicationDbContext context) : ICommandHandler<UpdateServiceEventTypeCommand, Result<UpdatedResult>>
{
  public async Task<Result<UpdatedResult>> Handle(UpdateServiceEventTypeCommand command, CancellationToken cancellationToken)
  {
    var type = await context.LoadEventTypeAsync(command.Id, cancellationToken);

    if (ServiceEventNames.IsSystem(type.Name) && !string.Equals(type.Name, command.Type.Name.Trim(), StringComparison.Ordinal))
      return Result<UpdatedResult>.Failure($"'{type.Name}' is the event HR actions are recorded under; it cannot be renamed.");

    type.Update(command.Type.Name, command.Type.Category);

    if (await context.ServiceEventTypes.AnyAsync(t => t.Id != type.Id && t.Name.ToLower() == type.Name.ToLower(), cancellationToken))
      return Result<UpdatedResult>.Failure($"Another service event type is named '{type.Name}'.");

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }
}

public class CreateRequestTypeHandler(IApplicationDbContext context) : ICommandHandler<CreateRequestTypeCommand, Result<CreatedResult>>
{
  public async Task<Result<CreatedResult>> Handle(CreateRequestTypeCommand command, CancellationToken cancellationToken)
  {
    var type = EmployeeRequestType.Create(EmployeeRequestTypeId.New(), command.Type.Name, command.Type.Code, command.Type.RequiresDocument);

    if (await context.RequestTypes.AnyAsync(t => t.Name.ToLower() == type.Name.ToLower() || (type.Code != null && t.Code == type.Code), cancellationToken))
      return Result<CreatedResult>.Failure($"A request type named '{type.Name}' or with the same code already exists.");

    context.RequestTypes.Add(type);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(type.Id);
  }
}

public class UpdateRequestTypeHandler(IApplicationDbContext context) : ICommandHandler<UpdateRequestTypeCommand, Result<UpdatedResult>>
{
  public async Task<Result<UpdatedResult>> Handle(UpdateRequestTypeCommand command, CancellationToken cancellationToken)
  {
    var typeId = EmployeeRequestTypeId.Of(command.Id);
    var type = await context.RequestTypes.FirstOrDefaultAsync(t => t.Id == typeId, cancellationToken)
      ?? throw new RequestTypeNotFoundException($"Request type {command.Id} was not found.");
    type.Update(command.Type.Name, command.Type.Code, command.Type.RequiresDocument);

    if (await context.RequestTypes.AnyAsync(t => t.Id != type.Id && (t.Name.ToLower() == type.Name.ToLower() || (type.Code != null && t.Code == type.Code)), cancellationToken))
      return Result<UpdatedResult>.Failure($"Another request type has the name '{type.Name}' or the same code.");

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }
}

public class UpdatePayScaleGradeHandler(IApplicationDbContext context) : ICommandHandler<UpdatePayScaleGradeCommand, Result<UpdatedResult>>
{
  public async Task<Result<UpdatedResult>> Handle(UpdatePayScaleGradeCommand command, CancellationToken cancellationToken)
  {
    var grade = await context.LoadGradeAsync(command.Id, cancellationToken);
    grade.Update(command.GradeName, command.IsActive);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }
}

public class SetCatalogActivationHandler(IApplicationDbContext context) : ICommandHandler<SetCatalogActivationCommand, Result<UpdatedResult>>
{
  public async Task<Result<UpdatedResult>> Handle(SetCatalogActivationCommand command, CancellationToken cancellationToken)
  {
    switch (command.Kind)
    {
      case CatalogKind.OrganizationUnitType:
        (await context.LoadUnitTypeAsync(command.Id, cancellationToken)).SetActive(command.IsActive);
        break;
      case CatalogKind.Location:
        (await context.LoadLocationAsync(command.Id, cancellationToken)).SetActive(command.IsActive);
        break;
      case CatalogKind.Designation:
        (await context.LoadDesignationAsync(command.Id, cancellationToken)).SetActive(command.IsActive);
        break;
      case CatalogKind.DocumentType:
        (await context.LoadDocumentTypeAsync(command.Id, cancellationToken)).SetActive(command.IsActive);
        break;
      case CatalogKind.RecruitmentMethod:
        var methodId = RecruitmentMethodId.Of(command.Id);
        var method = await context.RecruitmentMethods.FirstOrDefaultAsync(m => m.Id == methodId, cancellationToken)
          ?? throw new RecruitmentMethodNotFoundException($"Recruitment method {command.Id} was not found.");
        method.SetActive(command.IsActive);
        break;
      case CatalogKind.ServiceEventType:
        var eventType = await context.LoadEventTypeAsync(command.Id, cancellationToken);
        if (!command.IsActive && ServiceEventNames.IsSystem(eventType.Name))
          return Result<UpdatedResult>.Failure($"'{eventType.Name}' is the event HR actions are recorded under; it cannot be deactivated.");
        eventType.SetActive(command.IsActive);
        break;
      case CatalogKind.RequestType:
        var requestTypeId = EmployeeRequestTypeId.Of(command.Id);
        var requestType = await context.RequestTypes.FirstOrDefaultAsync(t => t.Id == requestTypeId, cancellationToken)
          ?? throw new RequestTypeNotFoundException($"Request type {command.Id} was not found.");
        requestType.SetActive(command.IsActive);
        break;
    }

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }
}
