/// Security policy, bound from the "Security" configuration section.
public sealed class SecurityOptions
{
  public const string SectionName = "Security";

  // ---- brute-force protection ----
  public int MaxFailedLoginAttempts { get; set; } = 5;
  public int LockoutMinutes { get; set; } = 15;

  // ---- password policy ----
  public int PasswordMinimumLength { get; set; } = 8;
  public bool PasswordRequireUppercase { get; set; } = true;
  public bool PasswordRequireLowercase { get; set; } = true;
  public bool PasswordRequireDigit { get; set; } = true;
  public bool PasswordRequireNonAlphanumeric { get; set; } = false;

  public TimeSpan LockoutDuration => TimeSpan.FromMinutes(LockoutMinutes);

  public PasswordPolicyOptions PasswordPolicy => new(
      PasswordMinimumLength,
      PasswordRequireUppercase,
      PasswordRequireLowercase,
      PasswordRequireDigit,
      PasswordRequireNonAlphanumeric);
}
