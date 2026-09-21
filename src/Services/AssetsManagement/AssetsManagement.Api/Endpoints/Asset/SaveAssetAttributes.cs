using System.Text.Json;

public sealed record SaveAssetAttributesRequest(Dictionary<string, JsonElement> Attributes, Guid? ChangedBy = null, string? ChangeReason = null);
public sealed record SaveAssetAttributesResponse(IReadOnlyDictionary<string, JsonElement> ExtraAttributes, DateTime? AttributesValidatedAt);

public class SaveAssetAttributes : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPut("/assets/{id}/attributes", async (Guid id, SaveAssetAttributesRequest request, ISender sender) =>
    {
      var result = await sender.Send(new SaveAssetAttributesCommand(id, request.Attributes, request.ChangedBy, request.ChangeReason));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(new SaveAssetAttributesResponse(result.Value!.ExtraAttributes, result.Value.AttributesValidatedAt));
    })
      .WithName("SaveAssetAttributes")
      .Produces<SaveAssetAttributesResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Save Asset Attributes")
      .WithDescription("Patches the dynamic attribute bag ({\"ram_gb\":16,\"features\":[\"WIFI\"]}). Keys present are set, JSON null removes a key, absent keys are kept. Values are validated, projected for filtering and written to history.");
  }
}
