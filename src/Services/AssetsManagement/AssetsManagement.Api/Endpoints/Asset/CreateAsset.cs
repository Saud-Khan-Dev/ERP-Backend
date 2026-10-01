public sealed record CreateAssetRequest(
  CreateAssetInput Asset,
  Guid? PerformedBy = null,
  AssetAcquisitionInput? Acquisition = null,
  DepreciationPlanInput? Depreciation = null);
public sealed record CreateAssetResponse(Guid Id, string AssetCode);

public class CreateAsset : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/assets", async (CreateAssetRequest request, ISender sender) =>
    {
      var result = await sender.Send(new CreateAssetCommand(request.Asset, request.PerformedBy, request.Acquisition, request.Depreciation));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      var response = result.Value.Adapt<CreateAssetResponse>();
      return Results.Created($"/assets/{response!.Id}", response);
    })
      .WithName("CreateAsset")
      .Produces<CreateAssetResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Create Asset")
      .WithDescription("Registers an asset in one transaction: the record (on a category without sub-categories), its extraAttributes (validated against GET /attribute-schema) and, when given, its acquisition and depreciation plan. Leave assetCode empty to have the next AST-###### code issued.");
  }
}
