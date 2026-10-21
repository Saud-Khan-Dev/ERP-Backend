public sealed record DeleteAssetRequest(string Reason);
public sealed record DeleteAssetResponse(bool IsSuccess);

public class DeleteAsset : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapDelete("/assets/{id}", async (Guid id, string? reason, [Microsoft.AspNetCore.Mvc.FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] DeleteAssetRequest? request, ISender sender) =>
    {
      var result = await sender.Send(new DeleteAssetCommand(id, request?.Reason ?? reason ?? ""));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<DeleteAssetResponse>());
    })
      .RequirePermission(PermissionCatalog.Assets.Delete)

      .WithName("DeleteAsset")
      .Produces<DeleteAssetResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Delete Asset")
      .WithDescription("Removes a record entered by mistake, with a reason (body {\"reason\": \"...\"} or ?reason=). Refused for a disposed asset (it stays as the record of the disposal) and for one with posted depreciation (reverse those months first). The record is kept out of every list and timeline.");
  }
}
