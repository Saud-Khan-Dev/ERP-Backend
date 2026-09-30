public sealed record CreatePropertyRequest(PropertyInput Property, Guid PropertyStatusId, DateOnly? StatusEffectiveFrom = null, MeasurementInput? Measurement = null);
public sealed record ChangeStatusRequest(Guid PropertyStatusId, DateOnly? EffectiveFrom = null, string? Reason = null, string? ReferenceNo = null);

/// The property register: core record, status history, measurements, area regularization.
public class PropertyEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    var properties = app.MapGroup("/properties").WithTags("Properties");

    properties.MapPost("/", async (CreatePropertyRequest request, ISender sender) =>
        (await sender.Send(new CreatePropertyCommand(request.Property, request.PropertyStatusId, request.StatusEffectiveFrom, request.Measurement)))
          .ToCreated(r => $"/properties/{r.Id}"))
      .RequirePermission(PermissionCatalog.Property.Create)
      .WithName("CreateProperty")
      .Produces<CreatePropertyCommandResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Register Property")
      .WithDescription("Registers a property. PROP-00001 is generated, the opening status is recorded in the status history and, optionally, the first measurement is saved.");

    properties.MapGet("/", async (
        ISender sender, int? pageIndex, int? pageSize, string? search, Guid? townId, Guid? propertyTypeId,
        Guid? propertyStatusId, Guid? propertyClassificationId, bool? includeInactive) =>
        (await sender.Send(new GetPropertiesQuery(
          new PaginationRequest(pageIndex ?? 0, pageSize ?? 20), search, townId, propertyTypeId,
          propertyStatusId, propertyClassificationId, includeInactive ?? false))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetProperties")
      .Produces<GetPropertiesQueryResult>()
      .WithSummary("Get Properties")
      .WithDescription("Paginated register. search matches the property code exactly, and the name or khasra number partially.");

    properties.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetPropertyQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetProperty")
      .Produces<GetPropertyQueryResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Property")
      .WithDescription("The property with its area summary (square feet) and current owners.");

    properties.MapPut("/{id:guid}", async (Guid id, PropertyInput property, ISender sender) =>
        (await sender.Send(new UpdatePropertyCommand(id, property))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("UpdateProperty")
      .Produces<UpdatePropertyCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update Property")
      .WithDescription("Core details only. The code never changes and the status has its own endpoint.");

    properties.MapPost("/{id:guid}/activate", async (Guid id, ISender sender) =>
        (await sender.Send(new SetPropertyActivationCommand(id, true))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("ActivateProperty")
      .Produces<SetPropertyActivationCommandResult>()
      .WithSummary("Activate Property");

    properties.MapPost("/{id:guid}/deactivate", async (Guid id, ISender sender) =>
        (await sender.Send(new SetPropertyActivationCommand(id, false))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Delete)
      .WithName("DeactivateProperty")
      .Produces<SetPropertyActivationCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Deactivate Property")
      .WithDescription("Nothing is hard-deleted: the property and its whole history stay on record.");

    // ---- status history ----

    properties.MapPost("/{id:guid}/status", async (Guid id, ChangeStatusRequest request, ISender sender) =>
        (await sender.Send(new ChangePropertyStatusCommand(id, request.PropertyStatusId, request.EffectiveFrom, request.Reason, request.ReferenceNo))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("ChangePropertyStatus")
      .Produces<ChangePropertyStatusCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Change Property Status")
      .WithDescription("Closes the current status period and opens a new one (e.g. Occupied → Encroached).");

    properties.MapGet("/{id:guid}/status-history", async (Guid id, ISender sender) =>
        (await sender.Send(new GetStatusHistoryQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetPropertyStatusHistory")
      .Produces<GetStatusHistoryQueryResult>()
      .WithSummary("Get Status History");

    // ---- area ----

    properties.MapPost("/{id:guid}/measurements", async (Guid id, MeasurementInput measurement, ISender sender) =>
        (await sender.Send(new RecordMeasurementCommand(id, measurement))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("RecordMeasurement")
      .Produces<RecordMeasurementCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Record Measurement")
      .WithDescription("A new survey of total and built-up area. The previous measurement is kept as history; square-feet values are computed from the unit.");

    properties.MapGet("/{id:guid}/measurements", async (Guid id, ISender sender) =>
        (await sender.Send(new GetMeasurementsQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetMeasurements")
      .Produces<GetMeasurementsQueryResult>()
      .WithSummary("Get Measurements");

    properties.MapGet("/{id:guid}/area", async (Guid id, ISender sender) =>
        (await sender.Send(new GetAreaSummaryQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetAreaSummary")
      .Produces<GetAreaSummaryQueryResult>()
      .WithSummary("Get Area Summary")
      .WithDescription("Current total and built-up area plus regularized additional area, in square feet.");

    properties.MapPost("/{id:guid}/regularizations", async (Guid id, RegularizationInput regularization, ISender sender) =>
        (await sender.Send(new OpenRegularizationCommand(id, regularization))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("OpenRegularization")
      .Produces<OpenRegularizationCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Open Area Regularization Case");

    properties.MapGet("/{id:guid}/regularizations", async (Guid id, bool? includeInactive, ISender sender) =>
        (await sender.Send(new GetRegularizationsQuery(id, includeInactive ?? false))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetRegularizations")
      .Produces<GetRegularizationsQueryResult>()
      .WithSummary("Get Area Regularizations");

    var regularizations = app.MapGroup("/regularizations").WithTags("Properties");

    regularizations.MapPut("/{id:guid}", async (Guid id, RegularizationCaseInput @case, ISender sender) =>
        (await sender.Send(new UpdateRegularizationCommand(id, @case))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("UpdateRegularization")
      .Produces<UpdateRegularizationCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Update Area Regularization Case")
      .WithDescription("Moves the case along (Applied → Pending → Regularized / Rejected). The area itself cannot change: a different area is a new case.");

    regularizations.MapPost("/{id:guid}/deactivate", async (Guid id, ISender sender) =>
        (await sender.Send(new DeactivateRegularizationCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("DeactivateRegularization")
      .Produces<DeactivateRegularizationCommandResult>()
      .WithSummary("Withdraw Area Regularization Case");
  }
}
