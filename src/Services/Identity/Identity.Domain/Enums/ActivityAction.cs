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
  SecuritySettingsUpdated,

  // ---- added with the System Administration overhaul (2026-10-21) ----
  /// The towns or offices an account works on.
  ScopeChanged,
  /// A separation-of-duty rule switched on or off (Super Administrator, with a reason).
  DutyRuleSwitched,
  OversightSettingsUpdated,
  AccessReviewStarted,
  AccessReviewDecided,
  AccessReviewClosed,
  /// The person changed their own password.
  PasswordChanged,
  /// The person signed out (this device, or every device).
  SignedOut,
  SignedOutOtherDevices,
  /// A refresh token was used twice: the sign-in's chain was ended (theft suspected).
  SessionTheftDetected,
  /// Wrong current passwords on "Change password" locked the account.
  AccountLockedByPasswordChange,
  /// The system's own changes at startup: new permissions given to roles, the first Super Administrator.
  PermissionsSeeded,
  SuperAdminSeeded,
  /// Dormant accounts switched off by the system (SecuritySettings.SwitchOffDormant).
  DormantAccountsSwitchedOff,
  /// An administrative act refused by a guard rail (worth knowing someone tried).
  ActRefused,
  /// A role or "Change roles" saved with two duties that should not meet, with the reason given.
  ConflictAccepted,
  /// The audit trail or an access report was exported.
  Exported,

  // ---- deleting one's own account (2026-10-27) ----
  /// The person asked for their own account to be deleted / took the request back.
  DeletionRequested,
  DeletionRequestWithdrawn,
  /// A senior turned the request down, with the reason (approving it writes UserDeleted).
  DeletionRequestRejected
}
