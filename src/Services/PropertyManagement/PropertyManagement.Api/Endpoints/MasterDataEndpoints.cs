/// Admin-editable lookup lists — every master table through one API. GET /masters lists the types
/// (towns, property-types, lease-statuses ...); the rest work on one type by its slug.
public class MasterDataEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    var group = app.MapGroup("/masters").WithTags("Master Data");

    group.MapGet("/", async (ISender sender) => (await sender.Send(new GetMasterTypesQuery())).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetMasterTypes")
      .Produces<GetMasterTypesQueryResult>()
      .WithSummary("List Master Data Types")
      .WithDescription("Every lookup list the property module uses, with the slug to call it by and any extra fields it carries.");

    group.MapGet("/{type}", async (string type, bool? includeInactive, ISender sender) =>
        (await sender.Send(new GetMastersQuery(type, includeInactive ?? false))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetMasters")
      .Produces<GetMastersQueryResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Master Values")
      .WithDescription("Values of one list (e.g. /masters/towns), in sort order. Inactive values are hidden unless includeInactive=true.");

    group.MapGet("/{type}/{id:guid}", async (string type, Guid id, ISender sender) =>
        (await sender.Send(new GetMasterQuery(type, id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetMaster")
      .Produces<GetMasterQueryResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Master Value");

    group.MapPost("/{type}", async (string type, MasterInput item, ISender sender) =>
        (await sender.Send(new CreateMasterCommand(type, item))).ToCreated(r => $"/masters/{type}/{r.Id}"))
      .RequirePermission(PermissionCatalog.PropertySetup.Create)
      .WithName("CreateMaster")
      .Produces<CreateMasterCommandResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Add Master Value")
      .WithDescription("Adds a value to a list. The code is permanent (it is what the system compares on); measurement units need factorToBase, document types may set storageFolder.");

    group.MapPut("/{type}/{id:guid}", async (string type, Guid id, UpdateMasterInput item, ISender sender) =>
        (await sender.Send(new UpdateMasterCommand(type, id, item))).ToOk())
      .RequirePermission(PermissionCatalog.PropertySetup.Edit)
      .WithName("UpdateMaster")
      .Produces<UpdateMasterCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update Master Value")
      .WithDescription("Renames or re-orders a value. The code cannot change.");

    group.MapPost("/{type}/{id:guid}/activate", async (string type, Guid id, ISender sender) =>
        (await sender.Send(new SetMasterActivationCommand(type, id, true))).ToOk())
      .RequirePermission(PermissionCatalog.PropertySetup.Edit)
      .WithName("ActivateMaster")
      .Produces<SetMasterActivationCommandResult>()
      .WithSummary("Activate Master Value");

    group.MapPost("/{type}/{id:guid}/deactivate", async (string type, Guid id, ISender sender) =>
        (await sender.Send(new SetMasterActivationCommand(type, id, false))).ToOk())
      .RequirePermission(PermissionCatalog.PropertySetup.Edit)
      .WithName("DeactivateMaster")
      .Produces<SetMasterActivationCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Deactivate Master Value")
      .WithDescription("Master values are never deleted: a deactivated value cannot be chosen for new records, existing records keep it.");
  }
}

/// Numbering schemes for generated codes (PROP-00001, OWN-00001, TRF-00001).
public class CodeSequenceEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    var group = app.MapGroup("/code-sequences").WithTags("Master Data");

    group.MapGet("/", async (ISender sender) => (await sender.Send(new GetCodeSequencesQuery())).ToOk())
      .RequirePermission(PermissionCatalog.PropertySetup.View)
      .WithName("GetCodeSequences")
      .Produces<GetCodeSequencesQueryResult>()
      .WithSummary("Get Code Sequences")
      .WithDescription("Each numbering scheme and the code it will issue next.");

    group.MapPut("/{key}", async (string key, CodeSequenceInput sequence, ISender sender) =>
        (await sender.Send(new UpdateCodeSequenceCommand(key, sequence))).ToOk())
      .RequirePermission(PermissionCatalog.PropertySetup.Edit)
      .WithName("UpdateCodeSequence")
      .Produces<UpdateCodeSequenceCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status409Conflict)
      .WithSummary("Update Code Sequence")
      .WithDescription("Changes prefix, separator, padding and next number (e.g. PROPERTY). Existing codes are never rewritten; the next number cannot point at a code already in use.");
  }
}
