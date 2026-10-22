/// Route requirements the shared .RequirePermission(code) does not cover on its own.
public static class AssetsAuthorization
{
  /// Anyone working in Assets - its register, its structure or its fields - may read the structure (classes, types,
  /// categories), the fields and the lists (statuses, locations, currencies, methods, event types): every screen of
  /// the module labels its records with them.
  public static readonly string[] AnyAssetsReader =
  [
    PermissionCatalog.Assets.View, PermissionCatalog.AssetTaxonomy.View, PermissionCatalog.AssetAttributes.View
  ];
}

public static class AssetsRouteExtensions
{
  /// A finance act: its own code, and ASSET_FINANCE.VIEW - nobody changes money they may not see (the Finance tab is
  /// shown only with VIEW, and the act's dialogs read the figures first).
  public static RouteHandlerBuilder RequireFinance(this RouteHandlerBuilder builder, string permissionCode) =>
    builder.RequirePermission(permissionCode).RequirePermission(PermissionCatalog.AssetFinance.View);

  /// Same, for an act any of several finance codes open.
  public static RouteHandlerBuilder RequireAnyFinance(this RouteHandlerBuilder builder, params string[] permissionCodes) =>
    builder.RequireAnyPermission(permissionCodes).RequirePermission(PermissionCatalog.AssetFinance.View);
}
