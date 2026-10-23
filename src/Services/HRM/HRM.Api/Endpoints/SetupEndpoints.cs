public sealed record ActivationRequest(bool IsActive);
public sealed record UpdatePayScaleGradeRequest(string? GradeName, bool IsActive);

/// HR catalogues: org unit types, locations, designations, BPS grades, document types, recruitment methods, employment
/// types, service event types and employee request types. Any signed-in user may read them (they label every HR screen); changing
/// them needs HR_SETUP. Values in use are deactivated; a value nobody used may be deleted.
public class SetupEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    var group = app.MapGroup("").WithTags("HR Setup");

    MapCatalog(group, "/org-unit-types", "OrganizationUnitType", CatalogKind.OrganizationUnitType,
      (includeInactive, sender) => sender.Send(new GetOrganizationUnitTypesQuery(includeInactive)),
      (OrganizationUnitTypeInput input, ISender sender) => sender.Send(new CreateOrganizationUnitTypeCommand(input)),
      (Guid id, OrganizationUnitTypeInput input, ISender sender) => sender.Send(new UpdateOrganizationUnitTypeCommand(id, input)));

    MapCatalog(group, "/locations", "Location", CatalogKind.Location,
      (includeInactive, sender) => sender.Send(new GetLocationsQuery(includeInactive)),
      (LocationInput input, ISender sender) => sender.Send(new CreateLocationCommand(input)),
      (Guid id, LocationInput input, ISender sender) => sender.Send(new UpdateLocationCommand(id, input)));

    MapCatalog(group, "/designations", "Designation", CatalogKind.Designation,
      (includeInactive, sender) => sender.Send(new GetDesignationsQuery(includeInactive)),
      (DesignationInput input, ISender sender) => sender.Send(new CreateDesignationCommand(input)),
      (Guid id, DesignationInput input, ISender sender) => sender.Send(new UpdateDesignationCommand(id, input)));

    MapCatalog(group, "/document-types", "DocumentType", CatalogKind.DocumentType,
      (includeInactive, sender) => sender.Send(new GetDocumentTypesQuery(includeInactive)),
      (DocumentTypeInput input, ISender sender) => sender.Send(new CreateDocumentTypeCommand(input)),
      (Guid id, DocumentTypeInput input, ISender sender) => sender.Send(new UpdateDocumentTypeCommand(id, input)));

    MapCatalog(group, "/recruitment-methods", "RecruitmentMethod", CatalogKind.RecruitmentMethod,
      (includeInactive, sender) => sender.Send(new GetRecruitmentMethodsQuery(includeInactive)),
      (RecruitmentMethodInput input, ISender sender) => sender.Send(new CreateRecruitmentMethodCommand(input)),
      (Guid id, RecruitmentMethodInput input, ISender sender) => sender.Send(new UpdateRecruitmentMethodCommand(id, input)));

    MapCatalog(group, "/employment-types", "EmploymentType", CatalogKind.EmploymentType,
      (includeInactive, sender) => sender.Send(new GetEmploymentTypesQuery(includeInactive)),
      (EmploymentTypeInput input, ISender sender) => sender.Send(new CreateEmploymentTypeCommand(input)),
      (Guid id, EmploymentTypeInput input, ISender sender) => sender.Send(new UpdateEmploymentTypeCommand(id, input)));

    MapCatalog(group, "/qualification-levels", "QualificationLevel", CatalogKind.QualificationLevel,
      (includeInactive, sender) => sender.Send(new GetQualificationLevelsQuery(includeInactive)),
      (QualificationLevelInput input, ISender sender) => sender.Send(new CreateQualificationLevelCommand(input)),
      (Guid id, QualificationLevelInput input, ISender sender) => sender.Send(new UpdateQualificationLevelCommand(id, input)));

    MapCatalog(group, "/family-relations", "FamilyRelation", CatalogKind.FamilyRelation,
      (includeInactive, sender) => sender.Send(new GetFamilyRelationsQuery(includeInactive)),
      (FamilyRelationInput input, ISender sender) => sender.Send(new CreateFamilyRelationCommand(input)),
      (Guid id, FamilyRelationInput input, ISender sender) => sender.Send(new UpdateFamilyRelationCommand(id, input)));

    MapCatalog(group, "/employment-methods", "EmploymentMethod", CatalogKind.EmploymentMethod,
      (includeInactive, sender) => sender.Send(new GetEmploymentMethodsQuery(includeInactive)),
      (EmploymentMethodInput input, ISender sender) => sender.Send(new CreateEmploymentMethodCommand(input)),
      (Guid id, EmploymentMethodInput input, ISender sender) => sender.Send(new UpdateEmploymentMethodCommand(id, input)));

    MapCatalog(group, "/adjustment-types", "AdjustmentType", CatalogKind.AdjustmentType,
      (includeInactive, sender) => sender.Send(new GetAdjustmentTypesQuery(includeInactive)),
      (AdjustmentTypeInput input, ISender sender) => sender.Send(new CreateAdjustmentTypeCommand(input)),
      (Guid id, AdjustmentTypeInput input, ISender sender) => sender.Send(new UpdateAdjustmentTypeCommand(id, input)), payroll: true);

    MapCatalog(group, "/service-event-types", "ServiceEventType", CatalogKind.ServiceEventType,
      (includeInactive, sender) => sender.Send(new GetServiceEventTypesQuery(includeInactive)),
      (ServiceEventTypeInput input, ISender sender) => sender.Send(new CreateServiceEventTypeCommand(input)),
      (Guid id, ServiceEventTypeInput input, ISender sender) => sender.Send(new UpdateServiceEventTypeCommand(id, input)));

    MapCatalog(group, "/request-types", "RequestType", CatalogKind.RequestType,
      (includeInactive, sender) => sender.Send(new GetRequestTypesQuery(includeInactive)),
      (RequestTypeInput input, ISender sender) => sender.Send(new CreateRequestTypeCommand(input)),
      (Guid id, RequestTypeInput input, ISender sender) => sender.Send(new UpdateRequestTypeCommand(id, input)));

    group.MapGet("/pay-scale-grades", async (bool? includeInactive, ISender sender) =>
        (await sender.Send(new GetPayScaleGradesQuery(includeInactive ?? false))).ToOk())
      .RequireAuthorization()
      .WithName("GetPayScaleGrades")
      .Produces<GetPayScaleGradesQueryResult>()
      .WithSummary("Get BPS Grades")
      .WithDescription("BPS 1-22. The rows are fixed; only the name and the active flag change.");

    group.MapPut("/pay-scale-grades/{id:guid}", async (Guid id, UpdatePayScaleGradeRequest request, ISender sender) =>
        (await sender.Send(new UpdatePayScaleGradeCommand(id, request.GradeName, request.IsActive))).ToOk())
      .RequirePermission(PermissionCatalog.HrSetup.Edit)
      .WithName("UpdatePayScaleGrade")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update BPS Grade");
  }

  /// GET (any signed-in user), POST, PUT /{id}, POST /{id}/activate|deactivate for one catalogue.
  private static void MapCatalog<TList, TInput>(
      RouteGroupBuilder group,
      string route,
      string name,
      CatalogKind kind,
      Func<bool, ISender, Task<Result<TList>>> list,
      Func<TInput, ISender, Task<Result<CreatedResult>>> create,
      Func<Guid, TInput, ISender, Task<Result<UpdatedResult>>> update,
      bool payroll = false)
  {
    // the HR lists are HR_SETUP's; a payroll list (adjustment types) is PAYROLL_SETUP's
    var (create_, edit, delete) = payroll
      ? (PermissionCatalog.PayrollSetup.Create, PermissionCatalog.PayrollSetup.Edit, PermissionCatalog.PayrollSetup.Delete)
      : (PermissionCatalog.HrSetup.Create, PermissionCatalog.HrSetup.Edit, PermissionCatalog.HrSetup.Delete);

    group.MapGet(route, async (bool? includeInactive, ISender sender) => (await list(includeInactive ?? false, sender)).ToOk())
      .RequireAuthorization()
      .WithName($"Get{name}s")
      .Produces<TList>()
      .WithSummary($"Get {name}s")
      .WithDescription("Active values only unless includeInactive=true.");

    group.MapPost(route, async (TInput input, ISender sender) => (await create(input, sender)).ToCreated(r => $"{route}/{r.Id}"))
      .RequirePermission(create_)
      .WithName($"Create{name}")
      .Produces<CreatedResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary($"Create {name}");

    group.MapPut($"{route}/{{id:guid}}", async (Guid id, TInput input, ISender sender) => (await update(id, input, sender)).ToOk())
      .RequirePermission(edit)
      .WithName($"Update{name}")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary($"Update {name}");

    group.MapPost($"{route}/{{id:guid}}/activate", async (Guid id, ISender sender) =>
        (await sender.Send(new SetCatalogActivationCommand(kind, id, true))).ToOk())
      .RequirePermission(edit)
      .WithName($"Activate{name}")
      .Produces<UpdatedResult>()
      .WithSummary($"Activate {name}");

    group.MapPost($"{route}/{{id:guid}}/deactivate", async (Guid id, ISender sender) =>
        (await sender.Send(new SetCatalogActivationCommand(kind, id, false))).ToOk())
      .RequirePermission(edit)
      .WithName($"Deactivate{name}")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary($"Deactivate {name}")
      .WithDescription("The value stays on old records but is no longer offered.");

    group.MapDelete($"{route}/{{id:guid}}", async (Guid id, ISender sender) =>
        (await sender.Send(new DeleteCatalogValueCommand(kind, id))).ToOk())
      .RequirePermission(delete)
      .WithName($"Delete{name}")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .ProducesProblem(StatusCodes.Status409Conflict)
      .WithSummary($"Delete {name}")
      .WithDescription("Only a value nobody used (a mistyped one); a used value answers 409 with the reason - deactivate it instead.");
  }
}
