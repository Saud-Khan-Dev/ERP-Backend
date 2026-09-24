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
  bool IsActive,
  bool MustChangePassword,
  bool MfaEnabled,
  bool IsLockedOut,
  DateTime? LockedUntil,
  int FailedLoginAttempts,
  DateTime? LastLoginAt,
  string? LastLoginIp,
  DateTime? PasswordChangedAt,
  DateTime? EmailVerifiedAt,
  DateTime? CreatedAt,
  string? CreatedBy);

public sealed record UserListItemDto(
  Guid Id,
  string Username,
  string Email,
  string DisplayName,
  bool IsActive,
  bool IsLockedOut,
  DateTime? LastLoginAt,
  IReadOnlyList<string> Roles);

public sealed record RoleDto(
  Guid Id,
  string Code,
  string Name,
  string? Description,
  bool IsSystem,
  bool IsActive,
  int PermissionCount);

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
  bool IsLive);

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
  bool MustChangePassword,
  IReadOnlyList<string> Roles,
  IReadOnlyList<string> Permissions);

public static class IdentityMappings
{
  public static UserDto ToDto(this User x, DateTime now) => new(
    x.Id.Value, x.Username.Value, x.Email.Value, x.DisplayName.Value, x.EmployeeId, x.IsActive,
    x.MustChangePassword, x.MfaEnabled, x.IsLockedOut(now), x.LockedUntil, x.FailedLoginAttempts,
    x.LastLoginAt, x.LastLoginIp?.Value, x.PasswordChangedAt, x.EmailVerifiedAt, x.CreatedAt, x.CreatedBy);

  public static UserListItemDto ToListItemDto(this User x, DateTime now, IReadOnlyList<string> roles) => new(
    x.Id.Value, x.Username.Value, x.Email.Value, x.DisplayName.Value, x.IsActive, x.IsLockedOut(now),
    x.LastLoginAt, roles);

  public static RoleDto ToDto(this Role x, int permissionCount) => new(
    x.Id.Value, x.Code.Value, x.RoleName.Value, x.Description, x.IsSystem, x.IsActive, permissionCount);

  public static PermissionModuleDto ToDto(this PermissionModule x) => new(
    x.Id.Value, x.Code.Value, x.ModuleName.Value);

  public static PermissionDto ToDto(this Permission x, string moduleCode) => new(
    x.Id.Value, x.Code.Value, moduleCode, x.Action.ToString().ToUpperInvariant(),
    x.PermissionName.Value, x.Description, x.IsActive);

  public static SessionDto ToDto(this Session x, DateTime now) => new(
    x.Id.Value, x.UserId.Value, x.IpAddress?.Value, x.UserAgent, x.IssuedAt, x.ExpiresAt,
    x.RevokedAt, x.RevokedReason, x.IsActive(now));

  public static LoginAttemptDto ToDto(this LoginAttempt x) => new(
    x.Id.Value, x.UserId?.Value, x.AttemptedUsername, x.Succeeded, x.FailureReason,
    x.IpAddress?.Value, x.UserAgent, x.AttemptedAt);

  public static CurrentUserDto ToCurrentUserDto(this User x, PermissionResolver.ResolvedPermissions resolved) => new(
    x.Id.Value, x.Username.Value, x.Email.Value, x.DisplayName.Value, x.EmployeeId, x.MustChangePassword,
    resolved.RoleCodes.OrderBy(r => r, StringComparer.Ordinal).ToList(),
    resolved.PermissionCodes.OrderBy(p => p, StringComparer.Ordinal).ToList());
}
