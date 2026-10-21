public class GetFieldLibrary : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/field-library", async (ISender sender) =>
    {
      var result = await sender.Send(new GetFieldLibraryQuery());
      return Results.Ok(result.Value);
    })
      .RequireAnyPermission(AssetsAuthorization.AnyAssetsReader)

      .WithName("GetFieldLibrary")
      .Produces<GetFieldLibraryQueryResult>(StatusCodes.Status200OK)
      .WithSummary("Get Field Library")
      .WithDescription("Configuration > Fields: every field (switched-off ones too) with its choices, where it is placed (class, type or category, and how, including \"not used here\" marks) and how many assets hold a value for it - a field can be deleted only when none do.");
  }
}
