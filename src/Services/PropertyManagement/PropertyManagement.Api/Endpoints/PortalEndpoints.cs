/// The taxpayer portal's side of Property (/my): what the signed-in person holds. Any signed-in person may call it; an
/// account not linked to an owner record (Identity `PUT /users/{id}/owner-link`) gets `linked: false`. Read only.
public class PortalEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/my/properties", async (ISender sender) => (await sender.Send(new GetMyPropertiesQuery())).ToOk())
      .RequireSignedIn()
      .WithTags("Taxpayer portal").WithName("GetMyProperties")
      .Produces<MyPropertiesDto>()
      .WithSummary("My Properties")
      .WithDescription("The properties the signed-in taxpayer holds today - as owner (with the share), tenant or lessee (the agreement in force) "
        + "or allottee - with the name, address, town, type and the current areas in sq ft. The owner is the token's own claim.");
  }
}
