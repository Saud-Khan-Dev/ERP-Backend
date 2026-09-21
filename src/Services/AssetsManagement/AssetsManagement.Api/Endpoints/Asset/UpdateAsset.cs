public sealed record UpdateAssetRequest(UpdateAssetInput Asset, Guid? PerformedBy = null);
public sealed record UpdateAssetResponse(bool IsSuccess);

public class UpdateAsset : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPut("/assets/{id}", async (Guid id, UpdateAssetRequest request, ISender sender) =>
    {
      var result = await sender.Send(new UpdateAssetCommand(id, request.Asset, request.PerformedBy));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<UpdateAssetResponse>());
    })
      .WithName("UpdateAsset")
      .Produces<UpdateAssetResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .ProducesProblem(StatusCodes.Status409Conflict)
      .WithSummary("Update Asset")
      .WithDescription("Updates core fields, optionally re-classifies the asset and patches extraAttributes (JSON null removes a key).");
  }
}
