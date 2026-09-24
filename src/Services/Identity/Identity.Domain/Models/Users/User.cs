/// A login account. Owns credential state and the rules that decide whether a sign-in may proceed.
///
/// Deliberately NOT here: department, designation, salary, joining date — the HR profile belongs to
/// the HRM service. This aggregate only keeps <see cref="EmployeeId"/> as a reference to it.
public class User : Aggregate<UserId>
{
  public Username Username { get; private set; } = default!;
  public EmailAddress Email { get; private set; } = default!;
  public PasswordHash PasswordHash { get; private set; } = default!;

  /// Reference to the HRM employee record. No FK: the HRM service owns that table and may live in
  /// another schema/service entirely.
  public Guid? EmployeeId { get; private set; }

  /// Display name shown in admin screens and audit trails, so listing users does not require a
  /// cross-service call for every row.
  public Name DisplayName { get; private set; } = default!;

  public bool IsActive { get; private set; }
  public DateTime? EmailVerifiedAt { get; private set; }

  // ---- brute-force protection ----
  public int FailedLoginAttempts { get; private set; }
  public DateTime? LockedUntil { get; private set; }

  // ---- password hygiene ----
  public DateTime? PasswordChangedAt { get; private set; }
  public bool MustChangePassword { get; private set; }

  // ---- MFA ----
  public bool MfaEnabled { get; private set; }
  public MfaType? MfaType { get; private set; }
  /// Encrypted at rest by the infrastructure layer; never leaves the service.
  public string? MfaSecret { get; private set; }

  public DateTime? LastLoginAt { get; private set; }
  public IpAddress? LastLoginIp { get; private set; }

  // ---- soft delete, so audit history survives ----
  public DateTime? DeletedAt { get; private set; }
  public string? DeletedBy { get; private set; }

  public bool IsDeleted => DeletedAt.HasValue;

  public bool IsLockedOut(DateTime now) => LockedUntil.HasValue && LockedUntil.Value > now;

  public static User Create(
      UserId id,
      Username username,
      EmailAddress email,
      Name displayName,
      PasswordHash passwordHash,
      Guid? employeeId,
      bool mustChangePassword,
      DateTime now)
  {
    ArgumentNullException.ThrowIfNull(username);
    ArgumentNullException.ThrowIfNull(email);
    ArgumentNullException.ThrowIfNull(displayName);
    ArgumentNullException.ThrowIfNull(passwordHash);

    if (employeeId == Guid.Empty)
      throw new DomainException("Employee id cannot be empty.");

    return new User
    {
      Id = id,
      Username = username,
      Email = email,
      DisplayName = displayName,
      PasswordHash = passwordHash,
      EmployeeId = employeeId,
      IsActive = true,
      MustChangePassword = mustChangePassword,
      PasswordChangedAt = now
    };
  }

  public void UpdateProfile(EmailAddress email, Name displayName, Guid? employeeId)
  {
    ArgumentNullException.ThrowIfNull(email);
    ArgumentNullException.ThrowIfNull(displayName);
    EnsureNotDeleted();

    if (employeeId == Guid.Empty)
      throw new DomainException("Employee id cannot be empty.");

    Email = email;
    DisplayName = displayName;
    EmployeeId = employeeId;
  }

  /// Throws when this account may not sign in. Called only after the password has been verified,
  /// so the caller can distinguish "wrong password" from "account disabled" in the audit log while
  /// still returning one generic message to the client.
  public void EnsureCanSignIn(DateTime now)
  {
    if (IsDeleted)
      throw new AccountUnavailableException("This account no longer exists.");

    if (!IsActive)
      throw new AccountUnavailableException("This account is inactive. Contact your administrator.");

    if (IsLockedOut(now))
      throw new AccountUnavailableException($"This account is locked until {LockedUntil:u}. Contact your administrator.");
  }

  /// Records a failed sign-in and locks the account once the threshold is reached.
  /// Returns true when this attempt caused the lockout.
  public bool RegisterFailedLogin(int maxAttempts, TimeSpan lockoutDuration, DateTime now)
  {
    if (maxAttempts <= 0)
      throw new DomainException("maxAttempts must be greater than zero.");

    FailedLoginAttempts++;

    if (FailedLoginAttempts < maxAttempts)
      return false;

    LockedUntil = now.Add(lockoutDuration);
    FailedLoginAttempts = 0;
    return true;
  }

  public void RegisterSuccessfulLogin(IpAddress? ip, DateTime now)
  {
    FailedLoginAttempts = 0;
    LockedUntil = null;
    LastLoginAt = now;
    LastLoginIp = ip;
  }

  public void SetPassword(PasswordHash passwordHash, bool mustChangePassword, DateTime now)
  {
    ArgumentNullException.ThrowIfNull(passwordHash);
    EnsureNotDeleted();

    PasswordHash = passwordHash;
    PasswordChangedAt = now;
    MustChangePassword = mustChangePassword;
    // a password reset also clears a lockout: the credential that was being guessed is gone
    FailedLoginAttempts = 0;
    LockedUntil = null;
  }

  public void ForcePasswordChange()
  {
    EnsureNotDeleted();
    MustChangePassword = true;
  }

  public void Activate()
  {
    EnsureNotDeleted();
    IsActive = true;
  }

  public void Deactivate()
  {
    EnsureNotDeleted();
    IsActive = false;
  }

  public void Unlock()
  {
    EnsureNotDeleted();
    LockedUntil = null;
    FailedLoginAttempts = 0;
  }

  public void VerifyEmail(DateTime now)
  {
    EnsureNotDeleted();
    EmailVerifiedAt = now;
  }

  public void EnableMfa(MfaType type, string secret)
  {
    EnsureNotDeleted();

    if (string.IsNullOrWhiteSpace(secret))
      throw new DomainException("An MFA secret is required.");

    MfaEnabled = true;
    MfaType = type;
    MfaSecret = secret;
  }

  public void DisableMfa()
  {
    EnsureNotDeleted();
    MfaEnabled = false;
    MfaType = null;
    MfaSecret = null;
  }

  public void SoftDelete(string? deletedBy, DateTime now)
  {
    if (IsDeleted)
      throw new DomainException("This account has already been deleted.");

    DeletedAt = now;
    DeletedBy = deletedBy;
    IsActive = false;
  }

  private void EnsureNotDeleted()
  {
    if (IsDeleted)
      throw new DomainException("This account has been deleted.");
  }
}
