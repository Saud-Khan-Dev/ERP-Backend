/// Every change an administrator can make in the Administration module, recorded for accountability.
public enum ActivityAction
{
  // ---- accounts ----
  UserCreated,
  UserProfileUpdated,
  UserActivated,
  UserDeactivated,
  UserDeleted,
  UserUnlocked,
  PasswordReset,
  SessionsRevoked,
  AuthorizedOfficerSet,
  AuthorizedOfficerCleared,
  /// The account was linked to (or unlinked from) the person's HR record.
  EmployeeLinked,
  EmployeeUnlinked,
  UserRestored,

  // ---- access ----
  RoleAssigned,
  RoleRemoved,
  PermissionOverrideSet,
  PermissionOverrideRemoved,
  /// "Change roles": the whole set given and taken in one act.
  RolesChanged,

  // ---- roles ----
  RoleCreated,
  RoleUpdated,
  RoleDeleted,
  RolePermissionsAdded,
  RolePermissionRemoved,
  /// The role editor's save: every permission added and removed, in one line.
  RolePermissionsChanged,

  // ---- configuration ----
  /// Kept so older activity lines still read: Identity no longer issues employee codes (HR does).
  EmployeeCodeTemplateUpdated,
  SecuritySettingsUpdated
}
