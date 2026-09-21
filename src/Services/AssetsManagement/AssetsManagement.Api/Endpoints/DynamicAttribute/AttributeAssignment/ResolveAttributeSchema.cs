public sealed record ResolveAttributeSchemaResponse(IReadOnlyList<ResolvedAttributeDto> Attributes);

public class ResolveAttributeSchema : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/attribute-schema", async (
      ISender sender,
      Guid? assetId,
      Guid? assetClassId,
      Guid? assetTypeId,
      Guid? categoryId) =>
    {
      var result = await sender.Send(new ResolveAttributeSchemaQuery(assetId, assetClassId, assetTypeId, categoryId));
      return Results.Ok(new ResolveAttributeSchemaResponse(result.Value!.Attributes));
    })
      .WithName("ResolveAttributeSchema")
      .Produces<ResolveAttributeSchemaResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Resolve Attribute Schema")
      .WithDescription("The effective data-entry form after CLASS -> TYPE -> CATEGORY(root..leaf) -> ASSET resolution. Pass assetId for an existing asset, or class / type / category to preview a new one.");
  }
}
