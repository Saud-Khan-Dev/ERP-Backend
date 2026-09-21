public sealed record GetAssetAttributeHistoryResponse(PaginatedResult<AssetAttributeHistoryDto> History);

public class GetAssetAttributeHistory : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/assets/{id}/attribute-history", async (Guid id, ISender sender, int? pageIndex, int? pageSize, string? attributeCode) =>
    {
      var result = await sender.Send(new GetAssetAttributeHistoryQuery(id, new PaginationRequest(pageIndex ?? 0, pageSize ?? 20), attributeCode));
      return Results.Ok(new GetAssetAttributeHistoryResponse(result.Value!.History));
    })
      .WithName("GetAssetAttributeHistory")
      .Produces<GetAssetAttributeHistoryResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Asset Attribute History")
      .WithDescription("Who changed which dynamic field, when, from what to what.");
  }
}
