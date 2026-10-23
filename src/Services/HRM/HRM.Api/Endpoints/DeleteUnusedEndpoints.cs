/// DELETE for the lists and units that may be removed while nobody used them (409 with the reason otherwise).
public class DeleteUnusedEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    void Map(string route, string name, string permission, Func<Guid, object> command) =>
      app.MapDelete(route, async (Guid id, ISender sender) => ((Result<UpdatedResult>)(await sender.Send(command(id)))!).ToOk())
        .RequirePermission(permission)
        .WithTags("Setup")
        .WithName($"Delete{name}")
        .Produces<UpdatedResult>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .WithSummary($"Delete {name}")
        .WithDescription("Only while nobody used it (entered by mistake); otherwise 409 with the reason - deactivate or close it instead.");

    Map("/loan-types/{id:guid}", "LoanType", PermissionCatalog.PayrollSetup.Delete, id => new DeleteLoanTypeCommand(id));
    Map("/salary-components/{id:guid}", "SalaryComponent", PermissionCatalog.PayrollSetup.Delete, id => new DeleteSalaryComponentCommand(id));
    Map("/work-shifts/{id:guid}", "WorkShift", PermissionCatalog.Attendance.Delete, id => new DeleteWorkShiftCommand(id));
    Map("/org-units/{id:guid}", "OrgUnit", PermissionCatalog.HrSetup.Delete, id => new DeleteOrgUnitCommand(id));
  }
}
