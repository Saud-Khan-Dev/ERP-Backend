public sealed record GetAssetAssignmentsResponse(IReadOnlyList<AssetAssignmentDto> Assignments);

public class GetAssetAssignments : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/assets/{id}/assignments", async (Guid id, ISender sender, bool? onlyOpenLoans) =>
    {
      var result = await sender.Send(new GetAssetAssignmentsQuery(id, onlyOpenLoans ?? false));
      return Results.Ok(new GetAssetAssignmentsResponse(result.Value!.Assignments));
    })
      .WithName("GetAssetAssignments")
      .Produces<GetAssetAssignmentsResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Asset Assignments")
      .WithDescription("Assignment / transfer history, newest first.");
  }
}
