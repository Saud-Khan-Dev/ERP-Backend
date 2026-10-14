/// Route requirements that the shared .RequirePermission(code) does not cover.
public static class TaxAuthorization
{
  /// Anyone working in Taxation may read its configuration (heads, policies, templates, circles): the notice and
  /// assessment screens label everything with it.
  public static readonly string[] AnyTaxReader = [PermissionCatalog.Tax.View, PermissionCatalog.TaxSetup.View];

  /// The caller needs at least one of the permissions (401 without a token, 403 without any of them).
  public static RouteHandlerBuilder RequireAnyPermission(this RouteHandlerBuilder builder, params string[] permissionCodes) =>
      builder
        .RequireAuthorization(policy => policy
          .RequireAuthenticatedUser()
          .RequireAssertion(context => permissionCodes.Any(code => context.User.HasClaim(ErpClaimTypes.Permission, code))))
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
}
