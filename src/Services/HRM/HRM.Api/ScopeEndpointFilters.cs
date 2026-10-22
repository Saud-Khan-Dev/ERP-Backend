/// Route groups whose reads are never limited by the caller's office scope.
public static class ScopeEndpointFilters
{
  /// Self-service (the person's own record) and the staff directory (every module's picker) see every employee,
  /// whatever offices the signed-in person works on.
  public static RouteGroupBuilder AllOffices(this RouteGroupBuilder group)
  {
    group.AddEndpointFilter(async (context, next) =>
    {
      var scope = context.HttpContext.RequestServices.GetRequiredService<EmployeeScope>();
      using (scope.Unrestricted())
        return await next(context);
    });
    return group;
  }
}
