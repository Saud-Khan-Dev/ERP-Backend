/// Bootstrap settings, bound from the "Seed" configuration section (see Identity.Api/seed.json).
///
/// The first Super Admin has to come from somewhere, and it must not be a public endpoint — that
/// would be exactly the self-registration hole the whole design exists to prevent. So it is seeded
/// from configuration or environment variables instead.
public sealed class SeedOptions
{
  public const string SectionName = "Seed";

  /// Runs the permission-catalogue and Super Admin seeders at startup.
  public bool Enabled { get; set; } = true;

  public string SuperAdminUsername { get; set; } = "superadmin";

  public string SuperAdminEmail { get; set; } = "superadmin@erp.local";

  public string SuperAdminDisplayName { get; set; } = "Super Administrator";

  /// Leave empty to have one generated and written to the startup log once.
  /// Set it through Seed__SuperAdminPassword in any environment that is not a developer machine.
  public string? SuperAdminPassword { get; set; }

  /// Whether the seeded account must pick a new password at first sign-in.
  /// The shared development credentials in seed.json turn this off so the account is usable
  /// straight away; a real deployment should leave it on.
  public bool SuperAdminMustChangePassword { get; set; } = true;
}
