/// Permission sets several Taxation routes accept (used with the shared `.RequireAnyPermission(...)`).
public static class TaxAuthorization
{
  /// Anyone working in Taxation may read its configuration (heads, policies, templates, circles): the notice and
  /// assessment screens label everything with it.
  public static readonly string[] AnyTaxReader = [PermissionCatalog.Tax.View, PermissionCatalog.TaxSetup.View];

  /// Reading a clearance certificate: Taxation, and Property's officers who check one before a transfer or a plan.
  public static readonly string[] ClearanceReader = [PermissionCatalog.Tax.View, PermissionCatalog.Property.Approve];

  /// Using up a clearance: Taxation's officers who issue clearances, and Property's officers when the transfer or plan is
  /// approved with it (the audit trail names who used it).
  public static readonly string[] ClearanceConsumer = [PermissionCatalog.Tax.Issue, PermissionCatalog.Property.Approve];

  /// Noting an export or a print in the trail (POST /audit/exports): registers, reports, notices, receipts, certificates.
  public static readonly string[] Exporter = [PermissionCatalog.Tax.Export];
}
