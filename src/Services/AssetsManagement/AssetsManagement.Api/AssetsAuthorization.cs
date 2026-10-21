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
