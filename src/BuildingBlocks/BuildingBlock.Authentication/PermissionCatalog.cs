/// The single source of truth for every permission in the ERP.
///
/// A permission code is always MODULE.ACTION — never free text. Business services reference the
/// constants below when protecting an endpoint; the Identity service seeds its `permission` table
/// from <see cref="All"/>, so the two can never drift apart.
public static class PermissionCatalog
{
  // ---- actions ----
  public static class Actions
  {
    public const string View = "VIEW";
    public const string Create = "CREATE";
    public const string Edit = "EDIT";
    public const string Delete = "DELETE";
    public const string Approve = "APPROVE";
    public const string Post = "POST";
    public const string Assign = "ASSIGN";
    public const string Revoke = "REVOKE";
  }

  // ---- modules ----
  public static class Modules
  {
    public const string Assets = "ASSETS";
    public const string AssetTaxonomy = "ASSET_TAXONOMY";
    public const string AssetAttributes = "ASSET_ATTRIBUTES";
    public const string AssetFinance = "ASSET_FINANCE";
    public const string Inventory = "INVENTORY";
    public const string Procurement = "PROCUREMENT";
    public const string Property = "PROPERTY";
    public const string PropertySetup = "PROPERTY_SETUP";
    public const string IamUsers = "IAM_USERS";
    public const string IamRoles = "IAM_ROLES";
    public const string IamPermissions = "IAM_PERMISSIONS";
    public const string IamSecurity = "IAM_SECURITY";
    public const string Hr = "HR";
    public const string HrSetup = "HR_SETUP";
    public const string Attendance = "ATTENDANCE";
    public const string Payroll = "PAYROLL";
  }

  // ---- permission codes, grouped by module ----

  /// Assets, attachments, assignments/transfers, lifecycle events, status changes.
  public static class Assets
  {
    public const string View = "ASSETS.VIEW";
    public const string Create = "ASSETS.CREATE";
    public const string Edit = "ASSETS.EDIT";
    public const string Delete = "ASSETS.DELETE";
  }

  /// Asset classes, types and the category tree.
  public static class AssetTaxonomy
  {
    public const string View = "ASSET_TAXONOMY.VIEW";
    public const string Create = "ASSET_TAXONOMY.CREATE";
    public const string Edit = "ASSET_TAXONOMY.EDIT";
    public const string Delete = "ASSET_TAXONOMY.DELETE";
  }

  /// Option sets, attribute groups, definitions and assignments — the dynamic attribute engine.
  public static class AssetAttributes
  {
    public const string View = "ASSET_ATTRIBUTES.VIEW";
    public const string Create = "ASSET_ATTRIBUTES.CREATE";
    public const string Edit = "ASSET_ATTRIBUTES.EDIT";
    public const string Delete = "ASSET_ATTRIBUTES.DELETE";
  }

  /// Acquisition, depreciation, valuation and disposal.
  public static class AssetFinance
  {
    public const string View = "ASSET_FINANCE.VIEW";
    public const string Create = "ASSET_FINANCE.CREATE";
    public const string Edit = "ASSET_FINANCE.EDIT";
    public const string Delete = "ASSET_FINANCE.DELETE";
    /// Post a depreciation entry to the ledger.
    public const string Post = "ASSET_FINANCE.POST";
    /// Approve a disposal.
    public const string Approve = "ASSET_FINANCE.APPROVE";
  }

  public static class Inventory
  {
    public const string View = "INVENTORY.VIEW";
    public const string Create = "INVENTORY.CREATE";
    public const string Edit = "INVENTORY.EDIT";
    public const string Delete = "INVENTORY.DELETE";
  }

  public static class Procurement
  {
    public const string View = "PROCUREMENT.VIEW";
    public const string Create = "PROCUREMENT.CREATE";
    public const string Edit = "PROCUREMENT.EDIT";
    public const string Delete = "PROCUREMENT.DELETE";
    public const string Approve = "PROCUREMENT.APPROVE";
  }

  /// Properties and everything recorded against them: status, area, owners, ownership, transfers,
  /// encumbrances, documents, custom-field values.
  public static class Property
  {
    public const string View = "PROPERTY.VIEW";
    public const string Create = "PROPERTY.CREATE";
    public const string Edit = "PROPERTY.EDIT";
    public const string Delete = "PROPERTY.DELETE";
    /// Approve and complete a transfer (the step that changes ownership).
    public const string Approve = "PROPERTY.APPROVE";
  }

  /// Property module settings: master data lists, code numbering, custom-field definitions.
  public static class PropertySetup
  {
    public const string View = "PROPERTY_SETUP.VIEW";
    public const string Create = "PROPERTY_SETUP.CREATE";
    public const string Edit = "PROPERTY_SETUP.EDIT";
    public const string Delete = "PROPERTY_SETUP.DELETE";
  }

  /// User account administration — Super Admin territory.
  public static class Users
  {
    public const string View = "IAM_USERS.VIEW";
    public const string Create = "IAM_USERS.CREATE";
    public const string Edit = "IAM_USERS.EDIT";
    public const string Delete = "IAM_USERS.DELETE";
    /// Grant or remove a role on a user.
    public const string Assign = "IAM_USERS.ASSIGN";
  }

  public static class Roles
  {
    public const string View = "IAM_ROLES.VIEW";
    public const string Create = "IAM_ROLES.CREATE";
    public const string Edit = "IAM_ROLES.EDIT";
    public const string Delete = "IAM_ROLES.DELETE";
  }

  public static class Permissions
  {
    public const string View = "IAM_PERMISSIONS.VIEW";
    /// Attach a permission to a role, or set a per-user override.
    public const string Assign = "IAM_PERMISSIONS.ASSIGN";
  }

  /// Login history, active sessions, forced logout.
  public static class Security
  {
    public const string View = "IAM_SECURITY.VIEW";
    public const string Edit = "IAM_SECURITY.EDIT";
    public const string Revoke = "IAM_SECURITY.REVOKE";
  }

  /// Employees and everything on their file: personal details, documents, education, posts held, service history,
  /// HR actions, separations, performance reviews, tasks and requests.
  public static class Hr
  {
    public const string View = "HR.VIEW";
    public const string Create = "HR.CREATE";
    public const string Edit = "HR.EDIT";
    public const string Delete = "HR.DELETE";
    /// Approve an HR action (appointment, transfer, promotion ...), verify documents, decide employee requests.
    public const string Approve = "HR.APPROVE";
  }

  /// HR setup: organization structure, designations, pay scales, sanctioned posts and the HR catalogues.
  public static class HrSetup
  {
    public const string View = "HR_SETUP.VIEW";
    public const string Create = "HR_SETUP.CREATE";
    public const string Edit = "HR_SETUP.EDIT";
    public const string Delete = "HR_SETUP.DELETE";
  }

  /// Shifts, holidays, attendance, and leave (types, entitlements, applications, the leave ledger).
  public static class Attendance
  {
    public const string View = "ATTENDANCE.VIEW";
    public const string Create = "ATTENDANCE.CREATE";
    public const string Edit = "ATTENDANCE.EDIT";
    public const string Delete = "ATTENDANCE.DELETE";
    /// Approve or reject leave.
    public const string Approve = "ATTENDANCE.APPROVE";
  }

  /// Money: salary components and rules, income tax, loans, GP Fund, bank accounts, payroll runs, pay slips, payments.
  public static class Payroll
  {
    public const string View = "PAYROLL.VIEW";
    public const string Create = "PAYROLL.CREATE";
    public const string Edit = "PAYROLL.EDIT";
    public const string Delete = "PAYROLL.DELETE";
    /// Approve a payroll run; sanction a loan.
    public const string Approve = "PAYROLL.APPROVE";
    /// Finalize (post) or reverse a payroll run and record payments.
    public const string Post = "PAYROLL.POST";
  }

  // =====================================================
  // REGISTRY — what the Identity seeder reads
  // =====================================================

  public sealed record ModuleDefinition(string Code, string Name);

  public sealed record PermissionDefinition(string Code, string Module, string Action, string Name);

  public static IReadOnlyList<ModuleDefinition> AllModules { get; } = new[]
  {
    new ModuleDefinition(Modules.Assets, "Assets"),
    new ModuleDefinition(Modules.AssetTaxonomy, "Asset Taxonomy"),
    new ModuleDefinition(Modules.AssetAttributes, "Asset Attributes"),
    new ModuleDefinition(Modules.AssetFinance, "Asset Finance"),
    new ModuleDefinition(Modules.Inventory, "Inventory"),
    new ModuleDefinition(Modules.Procurement, "Procurement"),
    new ModuleDefinition(Modules.Property, "Property"),
    new ModuleDefinition(Modules.PropertySetup, "Property Setup"),
    new ModuleDefinition(Modules.IamUsers, "User Administration"),
    new ModuleDefinition(Modules.IamRoles, "Role Administration"),
    new ModuleDefinition(Modules.IamPermissions, "Permission Administration"),
    new ModuleDefinition(Modules.IamSecurity, "Security & Audit"),
    new ModuleDefinition(Modules.Hr, "Human Resources"),
    new ModuleDefinition(Modules.HrSetup, "HR Setup"),
    new ModuleDefinition(Modules.Attendance, "Attendance & Leave"),
    new ModuleDefinition(Modules.Payroll, "Payroll"),
  };

  public static IReadOnlyList<PermissionDefinition> All { get; } = BuildAll();

  private static PermissionDefinition[] BuildAll()
  {
    var permissions = new List<PermissionDefinition>();

    void Crud(string module, string label, params string[] extraActions)
    {
      foreach (var action in new[] { Actions.View, Actions.Create, Actions.Edit, Actions.Delete }.Concat(extraActions))
        permissions.Add(new PermissionDefinition($"{module}.{action}", module, action, $"{Describe(action)} {label}"));
    }

    Crud(Modules.Assets, "assets");
    Crud(Modules.AssetTaxonomy, "asset taxonomy");
    Crud(Modules.AssetAttributes, "asset attributes");
    Crud(Modules.AssetFinance, "asset finance records", Actions.Post, Actions.Approve);
    Crud(Modules.Inventory, "inventory");
    Crud(Modules.Procurement, "purchases", Actions.Approve);
    Crud(Modules.Property, "properties", Actions.Approve);
    Crud(Modules.PropertySetup, "property setup");
    Crud(Modules.IamUsers, "user accounts", Actions.Assign);
    Crud(Modules.IamRoles, "roles");
    Crud(Modules.Hr, "employees", Actions.Approve);
    Crud(Modules.HrSetup, "HR setup");
    Crud(Modules.Attendance, "attendance and leave", Actions.Approve);
    Crud(Modules.Payroll, "payroll", Actions.Approve, Actions.Post);

    permissions.Add(new PermissionDefinition(Permissions.View, Modules.IamPermissions, Actions.View, "View permissions"));
    permissions.Add(new PermissionDefinition(Permissions.Assign, Modules.IamPermissions, Actions.Assign, "Assign permissions"));
    permissions.Add(new PermissionDefinition(Security.View, Modules.IamSecurity, Actions.View, "View security audit"));
    permissions.Add(new PermissionDefinition(Security.Edit, Modules.IamSecurity, Actions.Edit, "Edit security settings"));
    permissions.Add(new PermissionDefinition(Security.Revoke, Modules.IamSecurity, Actions.Revoke, "Revoke sessions"));

    return permissions.ToArray();
  }

  private static string Describe(string action) => action switch
  {
    Actions.View => "View",
    Actions.Create => "Create",
    Actions.Edit => "Edit",
    Actions.Delete => "Delete",
    Actions.Approve => "Approve",
    Actions.Post => "Post",
    Actions.Assign => "Assign",
    Actions.Revoke => "Revoke",
    _ => action
  };
}
