public sealed record GetAttributeDefinitionsResponse(IReadOnlyList<AttributeDefinitionDto> Definitions);

public class GetAttributeDefinitions : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/attribute-definitions", async (ISender sender, AttributeDataType? dataType, string? search, bool? includeInactive) =>
    {
      var result = await sender.Send(new GetAttributeDefinitionsQuery(dataType, search, includeInactive ?? false));
      return Results.Ok(new GetAttributeDefinitionsResponse(result.Value!.Definitions));
    })
      .WithName("GetAttributeDefinitions")
      .Produces<GetAttributeDefinitionsResponse>(StatusCodes.Status200OK)
      .WithSummary("Get Attribute Definitions")
      .WithDescription("The attribute dictionary, optionally filtered by data type or a code / name search.");
  }
}
