public sealed record CreateAssetValuationRequest(DateOnly ValuationDate, decimal Value, string CurrencyCode, string? ValuationMethod = null, Guid? ValuedBy = null, string? Notes = null);
public sealed record CreateAssetValuationResponse(Guid Id);

public class CreateAssetValuation : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/assets/{id}/valuations", async (Guid id, CreateAssetValuationRequest request, ISender sender) =>
    {
      var result = await sender.Send(new CreateAssetValuationCommand(id, request.ValuationDate, request.Value, request.CurrencyCode, request.ValuationMethod, request.ValuedBy, request.Notes));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      var response = result.Value.Adapt<CreateAssetValuationResponse>();
      return Results.Created($"/assets/{id}/valuations/{response!.Id}", response);
    })
      .WithName("CreateAssetValuation")
      .Produces<CreateAssetValuationResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Create Asset Valuation")
      .WithDescription("Records a valuation (one per asset per date).");
  }
}
