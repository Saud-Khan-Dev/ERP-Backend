public sealed record SavePropertyAttributesRequest(IReadOnlyList<AttributeValueInput> Values);

/// Custom fields: admins define extra fields for facts the schema has no column for; users fill them per property.
public class CustomFieldEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    var definitions = app.MapGroup("/attribute-definitions").WithTags("Custom Fields");

    definitions.MapGet("/", async (Guid? attributeGroupId, bool? includeInactive, Guid? propertyTypeId, Guid? classificationId, ISender sender) =>
        (await sender.Send(new GetAttributeDefinitionsQuery(attributeGroupId, includeInactive ?? false, propertyTypeId, classificationId))).ToOk())
      .RequireAnyPermission(PermissionCatalog.Property.View, PermissionCatalog.PropertySetup.View)
      .WithName("GetAttributeDefinitions")
      .Produces<GetAttributeDefinitionsQueryResult>()
      .WithSummary("Get Custom Fields")
      .WithDescription("valueCount: properties holding a value (0 = it can be deleted; then its kind of value and options in use are fixed). "
        + "propertyTypeId + classificationId: only the fields a registration of such a property shows (switched on, group switched on, applying to it).");

    definitions.MapDelete("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new DeleteAttributeDefinitionCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.PropertySetup.Delete)
      .WithName("DeleteAttributeDefinition")
      .Produces<DeleteAttributeDefinitionCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Delete Custom Field")
      .WithDescription("Only a field no property holds a value for; one with values is switched off instead (they stay readable).");

    definitions.MapPost("/", async (AttributeDefinitionInput definition, ISender sender) =>
        (await sender.Send(new SaveAttributeDefinitionCommand(null, definition))).ToCreated(r => $"/attribute-definitions/{r.Id}"))
      .RequirePermission(PermissionCatalog.PropertySetup.Create)
      .WithName("CreateAttributeDefinition")
      .Produces<SaveAttributeDefinitionCommandResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Create Custom Field")
      .WithDescription("Types: Text, Number, Decimal, Date, DateTime, Boolean, Dropdown, MultiSelect (the last two need optionsCsv). code is optional: "
        + "the service makes one from the label. appliesToPropertyTypeIds / appliesToClassificationIds: where it is shown (empty = every property).");

    definitions.MapPut("/{id:guid}", async (Guid id, AttributeDefinitionInput definition, ISender sender) =>
        (await sender.Send(new SaveAttributeDefinitionCommand(id, definition))).ToOk())
      .RequirePermission(PermissionCatalog.PropertySetup.Edit)
      .WithName("UpdateAttributeDefinition")
      .Produces<SaveAttributeDefinitionCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Update Custom Field")
      .WithDescription("The code cannot change once created. Once a property holds a value, the data type cannot change and an option in use stays.");

    definitions.MapPost("/{id:guid}/activate", async (Guid id, ISender sender) =>
        (await sender.Send(new SetAttributeDefinitionActivationCommand(id, true))).ToOk())
      .RequirePermission(PermissionCatalog.PropertySetup.Edit)
      .WithName("ActivateAttributeDefinition")
      .Produces<SetAttributeDefinitionActivationCommandResult>()
      .WithSummary("Activate Custom Field");

    definitions.MapPost("/{id:guid}/deactivate", async (Guid id, ISender sender) =>
        (await sender.Send(new SetAttributeDefinitionActivationCommand(id, false))).ToOk())
      .RequirePermission(PermissionCatalog.PropertySetup.Edit)
      .WithName("DeactivateAttributeDefinition")
      .Produces<SetAttributeDefinitionActivationCommandResult>()
      .WithSummary("Deactivate Custom Field")
      .WithDescription("Hides the field from the form; values already captured are kept.");

    var properties = app.MapGroup("/properties").WithTags("Custom Fields");

    properties.MapGet("/{id:guid}/attributes", async (Guid id, Guid? attributeGroupId, ISender sender) =>
        (await sender.Send(new GetPropertyAttributesQuery(id, attributeGroupId))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetPropertyAttributes")
      .Produces<GetPropertyAttributesQueryResult>()
      .WithSummary("Get Custom Field Values")
      .WithDescription("The property's custom-field form: the fields that apply to it (switched on, group switched on, its type and classification) with "
        + "value or default; then, isHidden, the values it holds for fields switched off or no longer applying (read-only).");

    properties.MapPut("/{id:guid}/attributes", async (Guid id, SavePropertyAttributesRequest request, ISender sender) =>
        (await sender.Send(new SavePropertyAttributesCommand(id, request.Values))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("SavePropertyAttributes")
      .Produces<SavePropertyAttributesCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Save Custom Field Values")
      .WithDescription("Each value is checked against its field's data type; null clears a field.");
  }
}
