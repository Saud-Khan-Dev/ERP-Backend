/// Permission sets several Taxation routes accept (used with the shared `.RequireAnyPermission(...)`).
public static class TaxAuthorization
{
  /// Anyone working in Taxation may read its configuration (heads, policies, templates, circles): the notice and
  /// assessment screens label everything with it.
  public static readonly string[] AnyTaxReader = [PermissionCatalog.Tax.View, PermissionCatalog.TaxSetup.View];

  /// Reading a clearance certificate: Taxation, and Property's officers who check one before a transfer or a plan.
  public static readonly string[] ClearanceReader = [PermissionCatalog.Tax.View, PermissionCatalog.Property.Approve];

  /// Using up a clearance: Taxation's approvers, and Property's officers when the transfer or plan is approved with it.
  public static readonly string[] ClearanceConsumer = [PermissionCatalog.Tax.Approve, PermissionCatalog.Property.Approve];
}
