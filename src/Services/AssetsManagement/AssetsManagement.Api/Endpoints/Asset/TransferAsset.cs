public sealed record TransferAssetRequest(
  Guid? ToDepartmentId,
  Guid? ToCustodianId,
  Guid? ToLocationId,
  DateTime? AssignmentDate = null,
  DateOnly? ExpectedReturnDate = null,
  string? Reason = null,
  bool ClearCustodian = false,
  bool ClearDepartment = false);

public sealed record TransferAssetResponse(Guid AssignmentId);

public class TransferAsset : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/assets/{id}/assignments", async (Guid id, TransferAssetRequest request, ISender sender) =>
    {
      var command = new TransferAssetCommand(
        id, request.ToDepartmentId, request.ToCustodianId, request.ToLocationId, request.AssignmentDate,
        request.ExpectedReturnDate, request.Reason, request.ClearCustodian, request.ClearDepartment);

      var result = await sender.Send(command);

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      var response = result.Value.Adapt<TransferAssetResponse>();
      return Results.Created($"/assets/{id}/assignments", response);
    })
      .RequirePermission(PermissionCatalog.Assets.Assign)

      .WithName("TransferAsset")
      .Produces<TransferAssetResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Transfer / Assign Asset")
      .WithDescription("Assigns the asset to a department / custodian / location and records the transfer. Set expectedReturnDate for a temporary issue (loan). toCustodianId / toDepartmentId are HRM ids (a person who has left is refused; a new person brings their unit unless one is chosen); leave them empty to keep the current one, or set clearCustodian / clearDepartment to leave nobody.");
  }
}
