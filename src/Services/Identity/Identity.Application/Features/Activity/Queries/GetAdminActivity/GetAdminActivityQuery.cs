public sealed record GetAdminActivityQueryResult(PaginatedResult<AdminActivityDto> Activities);

/// The administration activity trail: who changed what, filtered by actor, target, person (actor OR
/// target), action, kind of change (`ActivityKinds`) or date.
public sealed record GetAdminActivityQuery(
  PaginationRequest Pagination,
  Guid? ActorUserId = null,
  Guid? TargetId = null,
  string? Action = null,
  DateTime? From = null,
  DateTime? To = null,
  Guid? PersonId = null,
  string? Kind = null) : IQuery<Result<GetAdminActivityQueryResult>>;

/// The four kinds of change the Activity screen filters by.
public static class ActivityKinds
{
  public static readonly IReadOnlyDictionary<string, ActivityAction[]> Actions =
    new Dictionary<string, ActivityAction[]>(StringComparer.OrdinalIgnoreCase)
    {
      ["accounts"] =
      [
        ActivityAction.UserCreated, ActivityAction.UserProfileUpdated, ActivityAction.UserActivated, ActivityAction.UserDeactivated,
        ActivityAction.UserDeleted, ActivityAction.UserUnlocked, ActivityAction.PasswordReset, ActivityAction.SessionsRevoked,
        ActivityAction.AuthorizedOfficerSet, ActivityAction.AuthorizedOfficerCleared,
      ],
      ["access"] =
      [
        ActivityAction.RoleAssigned, ActivityAction.RoleRemoved, ActivityAction.PermissionOverrideSet, ActivityAction.PermissionOverrideRemoved,
      ],
      ["roles"] =
      [
        ActivityAction.RoleCreated, ActivityAction.RoleUpdated, ActivityAction.RoleDeleted, ActivityAction.RolePermissionsAdded,
        ActivityAction.RolePermissionRemoved, ActivityAction.RolePermissionsChanged,
      ],
      ["settings"] = [ActivityAction.EmployeeCodeTemplateUpdated, ActivityAction.SecuritySettingsUpdated],
    };
}
