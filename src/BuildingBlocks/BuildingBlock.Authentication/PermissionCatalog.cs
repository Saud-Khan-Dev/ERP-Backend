/// The single source of truth for every permission in the ERP.
///
/// A permission code is always MODULE.ACTION — never free text. Business services reference the
/// constants below when protecting an endpoint; the Identity service seeds its `permission` table
/// from <see cref="All"/>, so the two can never drift apart.
///
/// One code is one act an owner may want to give or withhold on its own: reversing a payment is not "approving", running
/// the daily job is not "editing", seeing a CNIC is not "seeing the employee". A code no endpoint checks is a promise the
/// system does not keep - every code below is enforced somewhere (the planned Inventory and Procurement modules excepted).
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
    /// Download a register or a report (CSV, print) - a copy leaves the system.
    public const string Export = "EXPORT";
    /// Start a run that touches every record at once (the daily job, year-end surcharge, a depreciation run, closing a day).
    public const string Run = "RUN";
    /// Reset another person's password (the act that takes over an account).
    public const string Reset = "RESET";
    /// Switch an account off and on, unlock it.
    public const string Disable = "DISABLE";
    /// Give or withhold one permission for one person (an exception to their roles).
    public const string Override = "OVERRIDE";
    /// Undo money already posted (a payment, a payroll run, a depreciation entry).
    public const string Reverse = "REVERSE";
    /// Let money owed go (waive a fee, a fine, a loan instalment; write a loan off).
    public const string Waive = "WAIVE";
    /// Issue and revoke a certificate (a tax clearance).
    public const string Issue = "ISSUE";
    /// Correct a balance by hand (opening arrears, a leave balance, a payslip adjustment, a salary override).
    public const string Adjust = "ADJUST";
    /// Decide a person's pay (a pay change, the next increment date).
    public const string Pay = "PAY";
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
    public const string PropertyLegal = "PROPERTY_LEGAL";
    public const string IamUsers = "IAM_USERS";
    public const string IamRoles = "IAM_ROLES";
    public const string IamPermissions = "IAM_PERMISSIONS";
    public const string IamSecurity = "IAM_SECURITY";
    public const string IamAudit = "IAM_AUDIT";
    public const string IamSystem = "IAM_SYSTEM";
    public const string Hr = "HR";
    public const string HrSetup = "HR_SETUP";
    public const string HrSensitive = "HR_SENSITIVE";
    public const string Attendance = "ATTENDANCE";
    public const string Payroll = "PAYROLL";
    public const string PayrollSetup = "PAYROLL_SETUP";
    public const string Tax = "TAX";
    public const string TaxSetup = "TAX_SETUP";
    public const string Dashboard = "DASHBOARD";
  }

  // ---- permission codes, grouped by module ----

  /// Assets, attachments, lifecycle events, status changes.
  public static class Assets
  {
    public const string View = "ASSETS.VIEW";
    public const string Create = "ASSETS.CREATE";
    public const string Edit = "ASSETS.EDIT";
    public const string Delete = "ASSETS.DELETE";
    /// Download the asset register or a report.
    public const string Export = "ASSETS.EXPORT";
    /// Hand an asset to a person or office, transfer it, take it back (custody).
    public const string Assign = "ASSETS.ASSIGN";
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
    /// Record a purchase, a depreciation entry, a valuation; propose a disposal.
    public const string Create = "ASSET_FINANCE.CREATE";
    public const string Edit = "ASSET_FINANCE.EDIT";
    public const string Delete = "ASSET_FINANCE.DELETE";
    /// Post a depreciation entry to the ledger.
    public const string Post = "ASSET_FINANCE.POST";
    /// Approve a disposal.
    public const string Approve = "ASSET_FINANCE.APPROVE";
    /// Run depreciation for every asset at once.
    public const string Run = "ASSET_FINANCE.RUN";
    /// Reverse a posted depreciation entry.
    public const string Reverse = "ASSET_FINANCE.REVERSE";
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
    /// Approve and complete a transfer (the step that changes ownership), allot, confirm an allottee, enter the owners of
    /// a property that was held before.
    public const string Approve = "PROPERTY.APPROVE";
    /// Download the property register or a report.
    public const string Export = "PROPERTY.EXPORT";
    /// Waive or recover a fine.
    public const string Waive = "PROPERTY.WAIVE";
  }

  /// Legal matters on a property: encroachment and violation notices, cases, hearings, appeals, ownership disputes,
  /// auction bids and cancelling a transfer - the acts a legal clerk does that an estate clerk does not.
  public static class PropertyLegal
  {
    /// See legal matters and confidential documents.
    public const string View = "PROPERTY_LEGAL.VIEW";
    /// Record notices, cases, hearings, parties, disputes, bids; cancel a transfer.
    public const string Edit = "PROPERTY_LEGAL.EDIT";
    /// Decide a case or appeal, resolve an encroachment, award an auction.
    public const string Approve = "PROPERTY_LEGAL.APPROVE";
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
    /// Edit an account's details, link it to an HR record.
    public const string Edit = "IAM_USERS.EDIT";
    public const string Delete = "IAM_USERS.DELETE";
    /// Grant or remove a role on a user; set what towns and offices they work on.
    public const string Assign = "IAM_USERS.ASSIGN";
    /// Reset another person's password.
    public const string Reset = "IAM_USERS.RESET";
    /// Switch an account off and on, unlock it.
    public const string Disable = "IAM_USERS.DISABLE";
    /// Download the list of accounts and the access reports.
    public const string Export = "IAM_USERS.EXPORT";
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
    /// Decide what a role can do.
    public const string Assign = "IAM_PERMISSIONS.ASSIGN";
    /// Give or withhold one permission for one person.
    public const string Override = "IAM_PERMISSIONS.OVERRIDE";
  }

  /// The sign-in security settings, and signing other people out.
  public static class Security
  {
    /// The security settings, the separation-of-duty rules, the devices people are signed in on.
    public const string View = "IAM_SECURITY.VIEW";
    public const string Edit = "IAM_SECURITY.EDIT";
    public const string Revoke = "IAM_SECURITY.REVOKE";
  }

  /// The audit trail of every module (who did what) and the sign-in history.
  public static class Audit
  {
    public const string View = "IAM_AUDIT.VIEW";
    public const string Export = "IAM_AUDIT.EXPORT";
  }

  /// The system page: services, jobs and storage.
  public static class SystemAdmin
  {
    public const string View = "IAM_SYSTEM.VIEW";
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
    /// Download the employee register or an HR report.
    public const string Export = "HR.EXPORT";
    /// Decide one person's pay: a pay change, the next increment date.
    public const string Pay = "HR.PAY";
    /// The annual increment and a pay revision for everyone at once.
    public const string Run = "HR.RUN";
  }

  /// The facts that identify a person or pay them: CNIC, date of birth, family, addresses, bank accounts, pay figures.
  /// HR.VIEW alone shows the employee without them.
  public static class HrSensitive
  {
    public const string View = "HR_SENSITIVE.VIEW";
    /// Change them - a CNIC, a date of birth, a salary bank account.
    public const string Edit = "HR_SENSITIVE.EDIT";
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
    /// Download attendance and leave reports.
    public const string Export = "ATTENDANCE.EXPORT";
    /// Close a day for everyone, grant entitlements in bulk, import attendance, close the leave year.
    public const string Run = "ATTENDANCE.RUN";
    /// Correct a leave balance by hand.
    public const string Adjust = "ATTENDANCE.ADJUST";
  }

  /// Money: payroll runs, pay slips, payments, loans, GP Fund, bank accounts, income tax.
  public static class Payroll
  {
    public const string View = "PAYROLL.VIEW";
    public const string Create = "PAYROLL.CREATE";
    public const string Edit = "PAYROLL.EDIT";
    public const string Delete = "PAYROLL.DELETE";
    /// Approve a payroll run; sanction a loan; hold or release a salary.
    public const string Approve = "PAYROLL.APPROVE";
    /// Finalize (post) a payroll run and record payments.
    public const string Post = "PAYROLL.POST";
    /// Download payroll, loan, tax and GP Fund reports; print slips and the bank advice.
    public const string Export = "PAYROLL.EXPORT";
    /// Reverse a finalized run, return or fail a payment.
    public const string Reverse = "PAYROLL.REVERSE";
    /// Waive a loan instalment, write a loan off.
    public const string Waive = "PAYROLL.WAIVE";
    /// Adjust a payslip, override one person's salary component, set a tax opening.
    public const string Adjust = "PAYROLL.ADJUST";
  }

  /// The rules payroll is computed by: salary components and rules, income-tax years and slabs, GP Fund interest rates,
  /// loan types - configuration, kept apart from the decisions on a person's pay.
  public static class PayrollSetup
  {
    public const string View = "PAYROLL_SETUP.VIEW";
    public const string Create = "PAYROLL_SETUP.CREATE";
    public const string Edit = "PAYROLL_SETUP.EDIT";
    public const string Delete = "PAYROLL_SETUP.DELETE";
  }

  /// Taxation: tax files (property facts, payers, overrides, holds, agreements), demands and demand runs, notices,
  /// surcharges, payments against the 1Link reference, fees and clearances.
  public static class Tax
  {
    public const string View = "TAX.VIEW";
    public const string Create = "TAX.CREATE";
    public const string Edit = "TAX.EDIT";
    public const string Delete = "TAX.DELETE";
    /// Approve a demand run or a surcharge run; set overrides, holds and categories; issue notices in bulk.
    public const string Approve = "TAX.APPROVE";
    /// Post payments received against a reference, and fee receipts.
    public const string Post = "TAX.POST";
    /// Download tax registers and reports; print notices and certificates.
    public const string Export = "TAX.EXPORT";
    /// Run the daily job, the year-end surcharge and bulk notice escalation.
    public const string Run = "TAX.RUN";
    /// Reverse a posted payment.
    public const string Reverse = "TAX.REVERSE";
    /// Waive or exempt a fee.
    public const string Waive = "TAX.WAIVE";
    /// Issue and revoke a tax clearance (no-dues certificate).
    public const string Issue = "TAX.ISSUE";
    /// Enter or correct opening arrears.
    public const string Adjust = "TAX.ADJUST";
  }

  /// Taxation setup: circles, heads, rate sets, settings, special policies, penalty rules, notice templates, letterhead
  /// and the notice table.
  public static class TaxSetup
  {
    public const string View = "TAX_SETUP.VIEW";
    public const string Create = "TAX_SETUP.CREATE";
    public const string Edit = "TAX_SETUP.EDIT";
    public const string Delete = "TAX_SETUP.DELETE";
    /// Approve a rate set (the Board's tariff).
    public const string Approve = "TAX_SETUP.APPROVE";
  }

  /// The organisation dashboard: one card per module the person may read, across GDA.
  public static class Dashboard
  {
    public const string View = "DASHBOARD.VIEW";
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
    new ModuleDefinition(Modules.PropertyLegal, "Property Legal Matters"),
    new ModuleDefinition(Modules.IamUsers, "User Administration"),
    new ModuleDefinition(Modules.IamRoles, "Role Administration"),
    new ModuleDefinition(Modules.IamPermissions, "Permission Administration"),
    new ModuleDefinition(Modules.IamSecurity, "Sign-in Security"),
    new ModuleDefinition(Modules.IamAudit, "Audit Trail"),
    new ModuleDefinition(Modules.IamSystem, "System"),
    new ModuleDefinition(Modules.Hr, "Human Resources"),
    new ModuleDefinition(Modules.HrSetup, "HR Setup"),
    new ModuleDefinition(Modules.HrSensitive, "Personal & Bank Details"),
    new ModuleDefinition(Modules.Attendance, "Attendance & Leave"),
    new ModuleDefinition(Modules.Payroll, "Payroll"),
    new ModuleDefinition(Modules.PayrollSetup, "Payroll Setup"),
    new ModuleDefinition(Modules.Tax, "Taxation"),
    new ModuleDefinition(Modules.TaxSetup, "Taxation Setup"),
    new ModuleDefinition(Modules.Dashboard, "Organisation Dashboard"),
  };

  public static IReadOnlyList<PermissionDefinition> All { get; } = BuildAll();

  private static PermissionDefinition[] BuildAll()
  {
    var permissions = new List<PermissionDefinition>();

    void Crud(string module, string label)
    {
      foreach (var action in new[] { Actions.View, Actions.Create, Actions.Edit, Actions.Delete })
        permissions.Add(new PermissionDefinition($"{module}.{action}", module, action, $"{Describe(action)} {label}"));
    }

    void One(string code, string name)
    {
      var parts = code.Split('.');
      permissions.Add(new PermissionDefinition(code, parts[0], parts[1], name));
    }

    Crud(Modules.Assets, "assets");
    One(Assets.Export, "Download asset registers and reports");
    One(Assets.Assign, "Hand over, transfer and take back assets");
    Crud(Modules.AssetTaxonomy, "asset taxonomy");
    Crud(Modules.AssetAttributes, "asset attributes");
    Crud(Modules.AssetFinance, "asset finance records");
    One(AssetFinance.Post, "Post depreciation entries");
    One(AssetFinance.Approve, "Approve asset disposals");
    One(AssetFinance.Run, "Run depreciation for all assets");
    One(AssetFinance.Reverse, "Reverse a posted depreciation entry");
    Crud(Modules.Inventory, "inventory");
    Crud(Modules.Procurement, "purchases");
    One(Procurement.Approve, "Approve purchases");
    Crud(Modules.Property, "properties");
    One(Property.Approve, "Approve transfers, allotments and ownership changes");
    One(Property.Export, "Download property registers and reports");
    One(Property.Waive, "Waive or recover fines");
    Crud(Modules.PropertySetup, "property setup");
    One(PropertyLegal.View, "View legal matters and confidential documents");
    One(PropertyLegal.Edit, "Record property legal matters");
    One(PropertyLegal.Approve, "Decide property legal matters");
    Crud(Modules.IamUsers, "user accounts");
    One(Users.Assign, "Give roles and set where people work");
    One(Users.Reset, "Reset other people's passwords");
    One(Users.Disable, "Switch accounts off and on, unlock them");
    One(Users.Export, "Download account lists and access reports");
    Crud(Modules.IamRoles, "roles");
    Crud(Modules.Hr, "employees");
    One(Hr.Approve, "Approve HR actions, documents and requests");
    One(Hr.Export, "Download employee registers and HR reports");
    One(Hr.Pay, "Decide a person's pay and next increment");
    One(Hr.Run, "Run the annual increment and pay revisions");
    One(HrSensitive.View, "View CNIC, family, addresses, bank and pay details");
    One(HrSensitive.Edit, "Change CNIC, date of birth and salary bank accounts");
    Crud(Modules.HrSetup, "HR setup");
    Crud(Modules.Attendance, "attendance and leave");
    One(Attendance.Approve, "Approve leave");
    One(Attendance.Export, "Download attendance and leave reports");
    One(Attendance.Run, "Close days, mark holidays, grant entitlements, import attendance, close the leave year");
    One(Attendance.Adjust, "Correct leave balances by hand");
    Crud(Modules.Payroll, "payroll");
    One(Payroll.Approve, "Approve payroll runs, sanction loans, hold salaries");
    One(Payroll.Post, "Finalize payroll runs and record payments");
    One(Payroll.Export, "Download payroll reports, print slips and bank advice");
    One(Payroll.Reverse, "Reverse payroll runs and payments");
    One(Payroll.Waive, "Waive loan instalments, write loans off");
    One(Payroll.Adjust, "Adjust payslips, override a salary, set tax openings");
    Crud(Modules.PayrollSetup, "payroll setup");
    Crud(Modules.Tax, "taxation records");
    One(Tax.Approve, "Approve demand and surcharge runs, overrides and holds");
    One(Tax.Post, "Post payments and fee receipts");
    One(Tax.Export, "Download tax registers, print notices and certificates");
    One(Tax.Run, "Run the daily job, year-end surcharge and notice escalation");
    One(Tax.Reverse, "Reverse a posted payment");
    One(Tax.Waive, "Waive or exempt fees");
    One(Tax.Issue, "Issue and revoke tax clearances");
    One(Tax.Adjust, "Enter and correct opening arrears");
    Crud(Modules.TaxSetup, "taxation setup");
    One(TaxSetup.Approve, "Approve the rate set in force");

    One(Permissions.View, "View what roles can do");
    One(Permissions.Assign, "Decide what roles can do");
    One(Permissions.Override, "Give one person an exception");
    One(Security.View, "View security settings, duty rules and signed-in devices");
    One(Security.Edit, "Edit sign-in security settings");
    One(Security.Revoke, "Sign other people out");
    One(Audit.View, "View the audit trail and sign-in history");
    One(Audit.Export, "Download the audit trail");
    One(SystemAdmin.View, "View the system page");
    One(Dashboard.View, "View the organisation dashboard");

    return permissions.ToArray();
  }

  // =====================================================
  // WHAT A NEW CODE INHERITS, AND WHAT SHOULD NOT MEET
  // =====================================================

  /// A code split off an older one, with the codes whose holders did its acts before the split. When the Identity
  /// seeder first creates the new permission it grants it to every role holding any of them, so nobody's work stops;
  /// an administrator then takes it away where it is not wanted. Never applied again once the permission exists.
  public static IReadOnlyDictionary<string, string[]> InheritedFrom { get; } = new Dictionary<string, string[]>(StringComparer.Ordinal)
  {
    [Assets.Export] = [Assets.View],
    [Assets.Assign] = [Assets.Edit],
    [AssetFinance.Run] = [AssetFinance.Create, AssetFinance.Post],
    [AssetFinance.Reverse] = [AssetFinance.Post],
    [Property.Export] = [Property.View],
    [Property.Waive] = [Property.Edit, Property.Approve],
    [PropertyLegal.View] = [Property.View],
    [PropertyLegal.Edit] = [Property.Edit],
    [PropertyLegal.Approve] = [Property.Approve],
    [Hr.Export] = [Hr.View],
    [Hr.Pay] = [Hr.Approve],
    [Hr.Run] = [Hr.Approve],
    [HrSensitive.View] = [Hr.View, Payroll.View],
    [HrSensitive.Edit] = [Hr.Edit, Payroll.Edit],
    [Attendance.Export] = [Attendance.View],
    [Attendance.Run] = [Attendance.Edit],
    [Attendance.Adjust] = [Attendance.Approve],
    [Payroll.Export] = [Payroll.View],
    [Payroll.Delete] = [Payroll.Edit],
    [Payroll.Reverse] = [Payroll.Post],
    [Payroll.Waive] = [Payroll.Approve],
    [Payroll.Adjust] = [Payroll.Create, Payroll.Edit],
    [PayrollSetup.View] = [Payroll.View],
    [PayrollSetup.Create] = [Payroll.Create, Payroll.Approve],
    [PayrollSetup.Edit] = [Payroll.Edit, Payroll.Approve],
    [PayrollSetup.Delete] = [Payroll.Delete],
    [Tax.Export] = [Tax.View],
    [Tax.Run] = [Tax.Edit],
    [Tax.Reverse] = [Tax.Approve],
    [Tax.Waive] = [Tax.Approve],
    [Tax.Issue] = [Tax.Approve],
    [Tax.Adjust] = [Tax.Create, Tax.Edit],
    [Users.Export] = [Users.View],
    [Users.Reset] = [Users.Edit],
    [Users.Disable] = [Users.Edit],
    [Permissions.Override] = [Permissions.Assign],
    [Audit.View] = [Security.View],
    [Audit.Export] = [Security.View],
    [SystemAdmin.View] = [Security.View],
  };

  /// Two permissions one person should not hold together (segregation of duties), with the reason in words. The role
  /// editor and "Change roles" warn before saving such a pair and ask why; the activity trail keeps the reason. The acts
  /// themselves are kept apart by the duty rules (<see cref="DutyRules"/>), which block.
  public sealed record Conflict(string First, string Second, string Reason);

  public static IReadOnlyList<Conflict> Conflicts { get; } =
  [
    new(Tax.Post, Tax.Reverse, "Posts payments and reverses them - a cashier should not reverse their own receipts."),
    new(Tax.Create, Tax.Waive, "Charges fees and waives them - charging and waiving should be two people."),
    new(TaxSetup.Edit, TaxSetup.Approve, "Drafts the rate set and approves it - the tariff should pass through two people."),
    new(Payroll.Approve, Payroll.Post, "Approves a pay run and pays it out - approval and payment should be two people."),
    new(Payroll.Create, Payroll.Approve, "Prepares a pay run and approves it - preparation and approval should be two people."),
    new(HrSensitive.Edit, Payroll.Post, "Changes salary bank accounts and pays salaries out - one person could redirect a salary."),
    new(Property.Edit, Property.Approve, "Starts a transfer and can approve it - a transfer should pass through two people."),
    new(AssetFinance.Create, AssetFinance.Approve, "Proposes a disposal and can approve it - disposal should pass through two people."),
    new(Users.Create, Permissions.Assign, "Registers accounts and decides what roles can do - one person could give themselves any access."),
    new(Users.Reset, Audit.Export, "Can take over any account and carry the audit trail out - keep the two apart."),
  ];

  private static string Describe(string action) => action switch
  {
    Actions.View => "View",
    Actions.Create => "Create",
    Actions.Edit => "Edit",
    Actions.Delete => "Delete",
    _ => action
  };
}
