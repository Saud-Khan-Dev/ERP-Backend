public sealed record CreateOwnerRequest(OwnerInput Owner, IReadOnlyList<ContactInput>? Contacts = null, IReadOnlyList<AddressInput>? Addresses = null);

/// People and organizations — owners, allottees, lessees, tenants, bidders, contractors.
public class OwnerEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    var owners = app.MapGroup("/owners").WithTags("Owners");

    owners.MapPost("/", async (CreateOwnerRequest request, ISender sender) =>
        (await sender.Send(new CreateOwnerCommand(request.Owner, request.Contacts, request.Addresses))).ToCreated(r => $"/owners/{r.Id}"))
      .RequirePermission(PermissionCatalog.Property.Create)
      .WithName("CreateOwner")
      .Produces<CreateOwnerCommandResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Register Owner")
      .WithDescription("OWN-00001 is generated. Refused when the CNIC or NTN is already registered — search first with GET /owners?cnic=...");

    owners.MapGet("/", async (ISender sender, int? pageIndex, int? pageSize, string? search, string? cnic, string? ntn, Guid? ownerTypeId, bool? includeInactive) =>
        (await sender.Send(new GetOwnersQuery(new PaginationRequest(pageIndex ?? 0, pageSize ?? 20), search, cnic, ntn, ownerTypeId, includeInactive ?? false))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetOwners")
      .Produces<GetOwnersQueryResult>()
      .WithSummary("Get Owners")
      .WithDescription("search matches owner code or CNIC exactly and the name partially.");

    owners.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetOwnerQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetOwner")
      .Produces<GetOwnerQueryResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Owner")
      .WithDescription("The owner with contacts and addresses.");

    owners.MapPut("/{id:guid}", async (Guid id, OwnerInput owner, ISender sender) =>
        (await sender.Send(new UpdateOwnerCommand(id, owner))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("UpdateOwner")
      .Produces<UpdateOwnerCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Update Owner");

    owners.MapPost("/{id:guid}/activate", async (Guid id, ISender sender) =>
        (await sender.Send(new SetOwnerActivationCommand(id, true))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("ActivateOwner")
      .Produces<SetOwnerActivationCommandResult>()
      .WithSummary("Activate Owner");

    owners.MapPost("/{id:guid}/deactivate", async (Guid id, ISender sender) =>
        (await sender.Send(new SetOwnerActivationCommand(id, false))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Delete)
      .WithName("DeactivateOwner")
      .Produces<SetOwnerActivationCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Deactivate Owner")
      .WithDescription("Refused while the owner still holds a current ownership share.");

    // ---- contacts & addresses ----

    owners.MapPost("/{id:guid}/contacts", async (Guid id, ContactInput contact, ISender sender) =>
        (await sender.Send(new SaveOwnerContactCommand(id, null, contact))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("AddOwnerContact")
      .Produces<SaveOwnerContactCommandResult>()
      .WithSummary("Add Contact")
      .WithDescription("The first contact becomes primary; marking another primary un-marks the previous one.");

    owners.MapPut("/{id:guid}/contacts/{contactId:guid}", async (Guid id, Guid contactId, ContactInput contact, ISender sender) =>
        (await sender.Send(new SaveOwnerContactCommand(id, contactId, contact))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("UpdateOwnerContact")
      .Produces<SaveOwnerContactCommandResult>()
      .WithSummary("Update Contact");

    owners.MapPost("/{id:guid}/contacts/{contactId:guid}/deactivate", async (Guid id, Guid contactId, ISender sender) =>
        (await sender.Send(new DeactivateOwnerContactCommand(id, contactId))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("DeactivateOwnerContact")
      .Produces<SaveOwnerContactCommandResult>()
      .WithSummary("Deactivate Contact");

    owners.MapPost("/{id:guid}/addresses", async (Guid id, AddressInput address, ISender sender) =>
        (await sender.Send(new SaveOwnerAddressCommand(id, null, address))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("AddOwnerAddress")
      .Produces<SaveOwnerAddressCommandResult>()
      .WithSummary("Add Address");

    owners.MapPut("/{id:guid}/addresses/{addressId:guid}", async (Guid id, Guid addressId, AddressInput address, ISender sender) =>
        (await sender.Send(new SaveOwnerAddressCommand(id, addressId, address))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("UpdateOwnerAddress")
      .Produces<SaveOwnerAddressCommandResult>()
      .WithSummary("Update Address");

    owners.MapPost("/{id:guid}/addresses/{addressId:guid}/deactivate", async (Guid id, Guid addressId, ISender sender) =>
        (await sender.Send(new DeactivateOwnerAddressCommand(id, addressId))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("DeactivateOwnerAddress")
      .Produces<SaveOwnerAddressCommandResult>()
      .WithSummary("Deactivate Address");

    owners.MapGet("/{id:guid}/ownerships", async (Guid id, bool? includeHistory, ISender sender) =>
        (await sender.Send(new GetOwnerOwnershipsQuery(id, includeHistory ?? false))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetOwnerOwnerships")
      .Produces<GetOwnershipsQueryResult>()
      .WithSummary("Get Owner's Properties")
      .WithDescription("What the owner holds now; includeHistory=true adds what they held before.");
  }
}
