/// Route requirements that the shared .RequirePermission(code) does not cover.
public static class HrmAuthorization
{
  /// Anyone working in HRM (any of the four modules) may read the structure: org units, posts, pay scales.
  public static readonly string[] AnyHrmReader =
  [
    PermissionCatalog.Hr.View, PermissionCatalog.HrSetup.View, PermissionCatalog.Attendance.View, PermissionCatalog.Payroll.View
  ];

  /// The caller needs at least one of the permissions (401 without a token, 403 without any of them).
  public static RouteHandlerBuilder RequireAnyPermission(this RouteHandlerBuilder builder, params string[] permissionCodes) =>
      builder
        .RequireAuthorization(policy => policy
          .RequireAuthenticatedUser()
          .RequireAssertion(context => permissionCodes.Any(code => context.User.HasClaim(ErpClaimTypes.Permission, code))))
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
}
