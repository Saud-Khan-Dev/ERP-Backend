public sealed record SavePropertyAttributesRequest(IReadOnlyList<AttributeValueInput> Values);

/// Custom fields: admins define extra fields for facts the schema has no column for; users fill them per property.
public class CustomFieldEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    var definitions = app.MapGroup("/attribute-definitions").WithTags("Custom Fields");

    definitions.MapGet("/", async (Guid? attributeGroupId, bool? includeInactive, ISender sender) =>
        (await sender.Send(new GetAttributeDefinitionsQuery(attributeGroupId, includeInactive ?? false))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetAttributeDefinitions")
      .Produces<GetAttributeDefinitionsQueryResult>()
      .WithSummary("Get Custom Fields");

    definitions.MapPost("/", async (AttributeDefinitionInput definition, ISender sender) =>
        (await sender.Send(new SaveAttributeDefinitionCommand(null, definition))).ToCreated(r => $"/attribute-definitions/{r.Id}"))
      .RequirePermission(PermissionCatalog.PropertySetup.Create)
      .WithName("CreateAttributeDefinition")
      .Produces<SaveAttributeDefinitionCommandResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Create Custom Field")
      .WithDescription("Types: Text, Number, Decimal, Date, DateTime, Boolean, Dropdown, MultiSelect (the last two need optionsCsv).");

    definitions.MapPut("/{id:guid}", async (Guid id, AttributeDefinitionInput definition, ISender sender) =>
        (await sender.Send(new SaveAttributeDefinitionCommand(id, definition))).ToOk())
      .RequirePermission(PermissionCatalog.PropertySetup.Edit)
      .WithName("UpdateAttributeDefinition")
      .Produces<SaveAttributeDefinitionCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Update Custom Field")
      .WithDescription("The code cannot change once created.");

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
      .WithDescription("The property's custom-field form: every active field with its value or default.");

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
