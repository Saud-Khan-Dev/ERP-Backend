public sealed record GetAssetStatusesResponse(IReadOnlyList<AssetStatusDto> Statuses);

public class GetAssetStatuses : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/asset-statuses", async (ISender sender, bool? includeInactive) =>
    {
      var result = await sender.Send(new GetAssetStatusesQuery(includeInactive ?? false));
      return Results.Ok(new GetAssetStatusesResponse(result.Value!.Statuses));
    })
      .WithName("GetAssetStatuses")
      .Produces<GetAssetStatusesResponse>(StatusCodes.Status200OK)
      .WithSummary("Get Asset Status list")
      .WithDescription("Get Asset Status list");
  }
}
