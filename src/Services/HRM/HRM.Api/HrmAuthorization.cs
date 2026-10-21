/// HRM's reader set for the shared .RequireAnyPermission(...) (BuildingBlock.Authentication).
public static class HrmAuthorization
{
  /// Anyone working in HRM (any of the four modules) may read the structure: org units, posts, pay scales.
  public static readonly string[] AnyHrmReader =
  [
    PermissionCatalog.Hr.View, PermissionCatalog.HrSetup.View, PermissionCatalog.Attendance.View, PermissionCatalog.Payroll.View
  ];
}
