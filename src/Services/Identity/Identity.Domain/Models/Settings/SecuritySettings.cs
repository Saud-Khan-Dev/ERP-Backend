/// The sign-in security policy for the whole system, editable by an administrator instead of
/// living in a configuration file that needs a redeploy to change:
///
///   - what a password must contain,
///   - how many wrong passwords lock an account and for how long,
///   - how long a signed-in session lasts.
///
/// One row for the whole system (a fixed id). Changing it affects sign-ins, password changes and
/// tokens issued from now on; tokens already issued keep the lifetime they were given.
public class SecuritySettings : Aggregate<SecuritySettingsId>
{
  /// Fixed id so the table can only ever hold the one policy.
  public static readonly Guid SingletonId = new("6d2c9a14-7e53-4b0a-8c21-1f7e4d9b0a52");

  // ---- guard rails: an admin cannot set a value that would lock everyone out or defeat the policy ----
  public const int MinPasswordLength = 6;
  public const int MaxPasswordLength = 128;
  public const int MinFailedAttempts = 3;
  public const int MaxFailedAttempts = 20;
  public const int MinLockoutMinutes = 1;
  public const int MaxLockoutMinutes = 1440;        // one day
  public const int MinAccessTokenMinutes = 5;
  public const int MaxAccessTokenMinutes = 240;      // four hours
  public const int MinRefreshTokenDays = 1;
  public const int MaxRefreshTokenDays = 90;

  // ---- password policy ----
  public int PasswordMinimumLength { get; private set; }
  public bool PasswordRequireUppercase { get; private set; }
  public bool PasswordRequireLowercase { get; private set; }
  public bool PasswordRequireDigit { get; private set; }
  public bool PasswordRequireNonAlphanumeric { get; private set; }

  // ---- brute-force lockout ----
  public int MaxFailedLoginAttempts { get; private set; }
  public int LockoutMinutes { get; private set; }

  // ---- session lifetimes ----
  public int AccessTokenMinutes { get; private set; }
  public int RefreshTokenDays { get; private set; }

  public PasswordPolicyOptions PasswordPolicy => new(
      PasswordMinimumLength,
      PasswordRequireUppercase,
      PasswordRequireLowercase,
      PasswordRequireDigit,
      PasswordRequireNonAlphanumeric);

  public TimeSpan LockoutDuration => TimeSpan.FromMinutes(LockoutMinutes);
  public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(AccessTokenMinutes);
  public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(RefreshTokenDays);

  public static SecuritySettings CreateDefault() => Create(
      SecuritySettingsId.Of(SingletonId),
      passwordMinimumLength: 8,
      passwordRequireUppercase: true,
      passwordRequireLowercase: true,
      passwordRequireDigit: true,
      passwordRequireNonAlphanumeric: false,
      maxFailedLoginAttempts: 5,
      lockoutMinutes: 15,
      accessTokenMinutes: 15,
      refreshTokenDays: 7);

  public static SecuritySettings Create(
      SecuritySettingsId id,
      int passwordMinimumLength,
      bool passwordRequireUppercase,
      bool passwordRequireLowercase,
      bool passwordRequireDigit,
      bool passwordRequireNonAlphanumeric,
      int maxFailedLoginAttempts,
      int lockoutMinutes,
      int accessTokenMinutes,
      int refreshTokenDays)
  {
    var settings = new SecuritySettings { Id = id };
    settings.Apply(
      passwordMinimumLength, passwordRequireUppercase, passwordRequireLowercase, passwordRequireDigit, passwordRequireNonAlphanumeric,
      maxFailedLoginAttempts, lockoutMinutes, accessTokenMinutes, refreshTokenDays);
    return settings;
  }

  public void Update(
      int passwordMinimumLength,
      bool passwordRequireUppercase,
      bool passwordRequireLowercase,
      bool passwordRequireDigit,
      bool passwordRequireNonAlphanumeric,
      int maxFailedLoginAttempts,
      int lockoutMinutes,
      int accessTokenMinutes,
      int refreshTokenDays) =>
    Apply(
      passwordMinimumLength, passwordRequireUppercase, passwordRequireLowercase, passwordRequireDigit, passwordRequireNonAlphanumeric,
      maxFailedLoginAttempts, lockoutMinutes, accessTokenMinutes, refreshTokenDays);

  private void Apply(
      int passwordMinimumLength,
      bool passwordRequireUppercase,
      bool passwordRequireLowercase,
      bool passwordRequireDigit,
      bool passwordRequireNonAlphanumeric,
      int maxFailedLoginAttempts,
      int lockoutMinutes,
      int accessTokenMinutes,
      int refreshTokenDays)
  {
    Between(passwordMinimumLength, MinPasswordLength, MaxPasswordLength, "The minimum password length");
    Between(maxFailedLoginAttempts, MinFailedAttempts, MaxFailedAttempts, "The number of failed attempts before lockout");
    Between(lockoutMinutes, MinLockoutMinutes, MaxLockoutMinutes, "The lockout duration in minutes");
    Between(accessTokenMinutes, MinAccessTokenMinutes, MaxAccessTokenMinutes, "The access-token lifetime in minutes");
    Between(refreshTokenDays, MinRefreshTokenDays, MaxRefreshTokenDays, "The sign-in lifetime in days");

    PasswordMinimumLength = passwordMinimumLength;
    PasswordRequireUppercase = passwordRequireUppercase;
    PasswordRequireLowercase = passwordRequireLowercase;
    PasswordRequireDigit = passwordRequireDigit;
    PasswordRequireNonAlphanumeric = passwordRequireNonAlphanumeric;
    MaxFailedLoginAttempts = maxFailedLoginAttempts;
    LockoutMinutes = lockoutMinutes;
    AccessTokenMinutes = accessTokenMinutes;
    RefreshTokenDays = refreshTokenDays;
  }

  private static void Between(int value, int min, int max, string what)
  {
    if (value < min || value > max)
      throw new DomainException($"{what} must be between {min} and {max}.");
  }
}
