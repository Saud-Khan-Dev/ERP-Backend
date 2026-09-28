/// The admin-editable template that employee codes (EMP-101) are issued from and checked against.
/// Part of user administration, so it carries the same permissions as editing accounts.
public class EmployeeCodeTemplateEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/employee-code-template", async (ISender sender) =>
    {
      var result = await sender.Send(new GetEmployeeCodeTemplateQuery());
      return Results.Ok(new GetEmployeeCodeTemplateResponse(result.Value!.Template));
    })
      .RequirePermission(PermissionCatalog.Users.View)
      .WithName("GetEmployeeCodeTemplate")
      .Produces<GetEmployeeCodeTemplateResponse>(StatusCodes.Status200OK)
      .WithSummary("Get Employee Code Template")
      .WithDescription("The current numbering scheme (e.g. EMP-###) and the code the next account will receive.");

    app.MapPut("/employee-code-template", async (UpdateEmployeeCodeTemplateRequest request, ISender sender) =>
    {
      var result = await sender.Send(new UpdateEmployeeCodeTemplateCommand(request.Template));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(new UpdateEmployeeCodeTemplateResponse(result.Value!.Template));
    })
      .RequirePermission(PermissionCatalog.Users.Edit)
      .WithName("UpdateEmployeeCodeTemplate")
      .Produces<UpdateEmployeeCodeTemplateResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status409Conflict)
      .WithSummary("Update Employee Code Template")
      .WithDescription("Changes the prefix, separator, zero-padding and next number. Existing codes are not rewritten, and the next number cannot be set to one already in use.");
  }
}

public sealed record GetEmployeeCodeTemplateResponse(EmployeeCodeTemplateDto Template);

public sealed record UpdateEmployeeCodeTemplateRequest(EmployeeCodeTemplateInput Template);
public sealed record UpdateEmployeeCodeTemplateResponse(EmployeeCodeTemplateDto Template);
