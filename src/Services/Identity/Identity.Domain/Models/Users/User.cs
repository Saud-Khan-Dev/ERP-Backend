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

  /// The employee number HR gave the person (EMP-101) - set together with <see cref="EmployeeId"/> when the account is
  /// linked to its HR record, never issued here. An older account may still carry a code Identity issued before HR
  /// owned the numbering: shown as "not linked" until the account is linked.
  public EmployeeCode? EmployeeCode { get; private set; }

  /// Display name shown in admin screens and audit trails, so listing users does not require a
  /// cross-service call for every row.
  public Name DisplayName { get; private set; } = default!;

  public bool IsActive { get; private set; }

  /// Officer authorized by the DG (GDA Act s.2(a-i), s.28, s.30): may impose fines and file complaints
  /// in court. A designation, not a role — it travels in the access token as its own claim.
  public bool IsAuthorizedOfficer { get; private set; }

  public DateTime? EmailVerifiedAt { get; private set; }

  // ---- brute-force protection ----
  public int FailedLoginAttempts { get; private set; }
  /// The last wrong password: failures older than the counting window are forgotten, so last month's typos do not
  /// add to today's.
  public DateTime? LastFailedLoginAt { get; private set; }
  public DateTime? LockedUntil { get; private set; }

  // ---- password hygiene ----
  public DateTime? PasswordChangedAt { get; private set; }
  public bool MustChangePassword { get; private set; }

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
      EmployeeCode? employeeCode,
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
      EmployeeCode = employeeCode,
      IsActive = true,
      MustChangePassword = mustChangePassword,
      PasswordChangedAt = now
    };
  }

  /// Name and email. The HR link and the employee code change only through <see cref="LinkEmployee"/>.
  public void UpdateProfile(EmailAddress email, Name displayName)
  {
    ArgumentNullException.ThrowIfNull(email);
    ArgumentNullException.ThrowIfNull(displayName);
    EnsureNotDeleted();

    Email = email;
    DisplayName = displayName;
  }

  /// Links the account to the person's HR record and takes HR's employee number as its code - or, with no record,
  /// unlinks it (an older code Identity issued is cleared with it: only HR numbers are shown from now on).
  public void LinkEmployee(Guid? employeeId, EmployeeCode? employeeCode)
  {
    EnsureNotDeleted();

    if (employeeId == Guid.Empty)
      throw new DomainException("Choose the person's HR record.");
    if (employeeId is not null && employeeCode is null)
      throw new DomainException("A linked account shows the person's HR employee number - it is missing.");

    EmployeeId = employeeId;
    EmployeeCode = employeeId is null ? null : employeeCode;
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

  /// Records a wrong password and locks the account once `maxAttempts` of them fall within the lockout window (the
  /// same span the lock lasts): failures older than that are forgotten. Returns true when this attempt locked it.
  public bool RegisterFailedLogin(int maxAttempts, TimeSpan lockoutDuration, DateTime now)
  {
    if (maxAttempts <= 0)
      throw new DomainException("maxAttempts must be greater than zero.");

    if (LastFailedLoginAt is not { } last || now - last > lockoutDuration)
      FailedLoginAttempts = 0;

    FailedLoginAttempts++;
    LastFailedLoginAt = now;

    if (FailedLoginAttempts < maxAttempts)
      return false;

    LockedUntil = now.Add(lockoutDuration);
    FailedLoginAttempts = 0;
    return true;
  }

  public void RegisterSuccessfulLogin(IpAddress? ip, DateTime now)
  {
    FailedLoginAttempts = 0;
    LastFailedLoginAt = null;
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
    LastFailedLoginAt = null;
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

  public void SetAuthorizedOfficer(bool isAuthorizedOfficer)
  {
    EnsureNotDeleted();

    if (isAuthorizedOfficer && !IsActive)
      throw new DomainException("An inactive account cannot be designated an authorized officer.");

    IsAuthorizedOfficer = isAuthorizedOfficer;
  }

  public void Unlock()
  {
    EnsureNotDeleted();
    LockedUntil = null;
    FailedLoginAttempts = 0;
    LastFailedLoginAt = null;
  }

  public void VerifyEmail(DateTime now)
  {
    EnsureNotDeleted();
    EmailVerifiedAt = now;
  }


  public void SoftDelete(string? deletedBy, DateTime now)
  {
    if (IsDeleted)
      throw new DomainException("This account has already been deleted.");

    DeletedAt = now;
    DeletedBy = deletedBy;
    IsActive = false;
  }

  /// Brings a deleted account back, switched off: an administrator reviews its roles and activates it.
  public void Restore()
  {
    if (!IsDeleted)
      throw new DomainException("This account is not deleted.");

    DeletedAt = null;
    DeletedBy = null;
    IsActive = false;
  }

  private void EnsureNotDeleted()
  {
    if (IsDeleted)
      throw new DomainException("This account has been deleted.");
  }
}
