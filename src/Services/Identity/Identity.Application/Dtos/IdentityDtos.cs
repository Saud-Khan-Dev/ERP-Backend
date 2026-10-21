// =====================================================
// DTOs
//
// Nothing here ever carries a password hash or an MFA secret — see principle P8 in
// docs/auth-service-requirements.md.
// =====================================================

public sealed record UserDto(
  Guid Id,
  string Username,
  string Email,
  string DisplayName,
  Guid? EmployeeId,
  string? EmployeeCode,
  bool IsActive,
  bool IsAuthorizedOfficer,
  bool MustChangePassword,
  bool IsLockedOut,
  DateTime? LockedUntil,
  int FailedLoginAttempts,
  DateTime? LastLoginAt,
  string? LastLoginIp,
  DateTime? PasswordChangedAt,
  DateTime? EmailVerifiedAt,
  DateTime? CreatedAt,
  string? CreatedBy,
  /// Deleted accounts are read only to show them (and restore them).
  bool IsDeleted,
  DateTime? DeletedAt,
  string? DeletedBy);

public sealed record UserListItemDto(
  Guid Id,
  string Username,
  string Email,
  string DisplayName,
  string? EmployeeCode,
  bool IsActive,
  bool IsLockedOut,
  DateTime? LastLoginAt,
  /// Role codes (kept for older callers); `RoleNames` is what a screen shows.
  IReadOnlyList<string> Roles,
  IReadOnlyList<string> RoleNames,
  Guid? EmployeeId,
  bool MustChangePassword,
  bool IsAuthorizedOfficer,
  /// A live grant of a switched-on role or a live Allow exception - false means the account can open nothing.
  bool HasAccess,
  DateTime? CreatedAt,
  bool IsDeleted);

public sealed record RoleDto(
  Guid Id,
  string Code,
  string Name,
  string? Description,
  bool IsSystem,
  bool IsActive,
  int PermissionCount,
  /// People holding it now: live grants on accounts that are not deleted (active or not).
  int HolderCount);

public sealed record PermissionModuleDto(Guid Id, string Code, string Name);

public sealed record PermissionDto(
  Guid Id,
  string Code,
  string Module,
  string Action,
  string Name,
  string? Description,
  bool IsActive);

public sealed record UserRoleDto(
  Guid RoleId,
  string RoleCode,
  string RoleName,
  DateTime AssignedAt,
  Guid? AssignedBy,
  DateTime? ExpiresAt,
  bool IsLive,
  /// A switched-off role gives nothing even while it is held.
  bool RoleIsActive);

public sealed record UserPermissionOverrideDto(
  Guid PermissionId,
  string PermissionCode,
  string Effect,
  DateTime GrantedAt,
  Guid? GrantedBy,
  DateTime? ExpiresAt,
  string? Reason,
  bool IsLive);

public sealed record SessionDto(
  Guid Id,
  Guid UserId,
  string? IpAddress,
  string? UserAgent,
  DateTime IssuedAt,
  DateTime ExpiresAt,
  DateTime? RevokedAt,
  string? RevokedReason,
  bool IsActive);

public sealed record LoginAttemptDto(
  Guid Id,
  Guid? UserId,
  string AttemptedUsername,
  bool Succeeded,
  string? FailureReason,
  string? IpAddress,
  string? UserAgent,
  DateTime AttemptedAt);

/// What a successful sign-in returns.
public sealed record AuthenticationResultDto(
  string AccessToken,
  DateTime AccessTokenExpiresAt,
  string RefreshToken,
  DateTime RefreshTokenExpiresAt,
  bool MustChangePassword,
  CurrentUserDto User);

/// The authenticated caller's own identity and effective permissions — what a UI needs to decide
/// which actions to show. The backend still enforces them independently.
public sealed record CurrentUserDto(
  Guid Id,
  string Username,
  string Email,
  string DisplayName,
  Guid? EmployeeId,
  string? EmployeeCode,
  bool IsAuthorizedOfficer,
  bool MustChangePassword,
  IReadOnlyList<string> Roles,
  IReadOnlyList<string> Permissions,
  /// The names of the roles in `Roles`, in the same order - what a screen shows.
  IReadOnlyList<string> RoleNames);

public sealed record SecuritySettingsDto(
  int PasswordMinimumLength,
  bool PasswordRequireUppercase,
  bool PasswordRequireLowercase,
  bool PasswordRequireDigit,
  bool PasswordRequireNonAlphanumeric,
  int MaxFailedLoginAttempts,
  int LockoutMinutes,
  int AccessTokenMinutes,
  int RefreshTokenDays);

public sealed record AdminActivityDto(
  Guid Id,
  DateTime OccurredAt,
  Guid ActorUserId,
  string ActorName,
  string Action,
  string TargetType,
  Guid? TargetId,
  string? TargetLabel,
  string? Detail,
  string? IpAddress,
  string? UserAgent);

public static class IdentityMappings
{
  public static UserDto ToDto(this User x, DateTime now) => new(
    x.Id.Value, x.Username.Value, x.Email.Value, x.DisplayName.Value, x.EmployeeId, x.EmployeeCode?.Value, x.IsActive, x.IsAuthorizedOfficer,
    x.MustChangePassword, x.IsLockedOut(now), x.LockedUntil, x.FailedLoginAttempts,
    x.LastLoginAt, x.LastLoginIp?.Value, x.PasswordChangedAt, x.EmailVerifiedAt, x.CreatedAt, x.CreatedBy,
    x.IsDeleted, x.DeletedAt, x.DeletedBy);

  public static UserListItemDto ToListItemDto(this User x, DateTime now, IReadOnlyList<string> roles, IReadOnlyList<string> roleNames, bool hasAccess) => new(
    x.Id.Value, x.Username.Value, x.Email.Value, x.DisplayName.Value, x.EmployeeCode?.Value, x.IsActive, x.IsLockedOut(now),
    x.LastLoginAt, roles, roleNames, x.EmployeeId, x.MustChangePassword, x.IsAuthorizedOfficer, hasAccess, x.CreatedAt, x.IsDeleted);

  public static RoleDto ToDto(this Role x, int permissionCount, int holderCount = 0) => new(
    x.Id.Value, x.Code.Value, x.RoleName.Value, x.Description, x.IsSystem, x.IsActive, permissionCount, holderCount);

  public static PermissionModuleDto ToDto(this PermissionModule x) => new(
    x.Id.Value, x.Code.Value, x.ModuleName.Value);

  public static PermissionDto ToDto(this Permission x, string moduleCode) => new(
    x.Id.Value, x.Code.Value, moduleCode, x.Action.ToString().ToUpperInvariant(),
    x.PermissionName.Value, x.Description, x.IsActive);

  public static SessionDto ToDto(this Session x, DateTime now) => new(
    x.Id.Value, x.UserId.Value, x.IpAddress?.Value, x.UserAgent, x.IssuedAt, x.ExpiresAt,
    x.RevokedAt, x.RevokedReason, x.IsActive(now));

  public static SecuritySettingsDto ToDto(this SecuritySettings x) => new(
    x.PasswordMinimumLength, x.PasswordRequireUppercase, x.PasswordRequireLowercase, x.PasswordRequireDigit, x.PasswordRequireNonAlphanumeric,
    x.MaxFailedLoginAttempts, x.LockoutMinutes, x.AccessTokenMinutes, x.RefreshTokenDays);

  public static LoginAttemptDto ToDto(this LoginAttempt x) => new(
    x.Id.Value, x.UserId?.Value, x.AttemptedUsername, x.Succeeded, x.FailureReason,
    x.IpAddress?.Value, x.UserAgent, x.AttemptedAt);

  public static AdminActivityDto ToDto(this AdminActivity x) => new(
    x.Id.Value, x.OccurredAt, x.ActorUserId.Value, x.ActorName, x.Action.ToString(), x.TargetType.ToString(),
    x.TargetId, x.TargetLabel, x.Detail, x.IpAddress?.Value, x.UserAgent);

  public static CurrentUserDto ToCurrentUserDto(this User x, PermissionResolver.ResolvedPermissions resolved, IReadOnlyDictionary<string, string>? roleNames = null)
  {
    var roles = resolved.RoleCodes.OrderBy(r => r, StringComparer.Ordinal).ToList();
    return new(
      x.Id.Value, x.Username.Value, x.Email.Value, x.DisplayName.Value, x.EmployeeId, x.EmployeeCode?.Value, x.IsAuthorizedOfficer, x.MustChangePassword,
      roles,
      resolved.PermissionCodes.OrderBy(p => p, StringComparer.Ordinal).ToList(),
      roles.Select(code => roleNames?.GetValueOrDefault(code) ?? code).ToList());
  }
}
