public sealed record GetNextAssetTagResponse(string AssetCode);

public class GetNextAssetTag : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/assets/next-tag", async (ISender sender) =>
    {
      var result = await sender.Send(new GetNextAssetTagQuery());
      return Results.Ok(new GetNextAssetTagResponse(result.Value!.AssetCode));
    })
      .WithName("GetNextAssetTag")
      .Produces<GetNextAssetTagResponse>(StatusCodes.Status200OK)
      .WithSummary("Get Next Asset Tag")
      .WithDescription("The tag (AST-000123) a registration that leaves the tag empty would get now. A preview, not a reservation.");
  }
}
