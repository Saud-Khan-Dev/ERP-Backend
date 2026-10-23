using FluentValidation;

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
        ActivityAction.EmployeeLinked, ActivityAction.EmployeeUnlinked, ActivityAction.UserRestored,
        ActivityAction.PasswordChanged, ActivityAction.SignedOut, ActivityAction.SignedOutOtherDevices, ActivityAction.SessionTheftDetected,
        ActivityAction.AccountLockedByPasswordChange, ActivityAction.DormantAccountsSwitchedOff, ActivityAction.SuperAdminSeeded,
      ],
      ["access"] =
      [
        ActivityAction.RoleAssigned, ActivityAction.RoleRemoved, ActivityAction.PermissionOverrideSet, ActivityAction.PermissionOverrideRemoved,
        ActivityAction.RolesChanged, ActivityAction.ScopeChanged, ActivityAction.ConflictAccepted, ActivityAction.ActRefused,
        ActivityAction.AccessReviewStarted, ActivityAction.AccessReviewDecided, ActivityAction.AccessReviewClosed,
      ],
      ["roles"] =
      [
        ActivityAction.RoleCreated, ActivityAction.RoleUpdated, ActivityAction.RoleDeleted, ActivityAction.RolePermissionsAdded,
        ActivityAction.RolePermissionRemoved, ActivityAction.RolePermissionsChanged, ActivityAction.PermissionsSeeded,
      ],
      ["settings"] =
      [
        ActivityAction.EmployeeCodeTemplateUpdated, ActivityAction.SecuritySettingsUpdated, ActivityAction.OversightSettingsUpdated,
        ActivityAction.DutyRuleSwitched, ActivityAction.Exported,
      ],
    };
}

public class GetAdminActivityQueryValidator : AbstractValidator<GetAdminActivityQuery>
{
  public GetAdminActivityQueryValidator()
  {
    RuleFor(x => x.Pagination.Pageindex).GreaterThanOrEqualTo(0).WithMessage("The page number cannot be negative.");
    RuleFor(x => x.Pagination.PageSize).InclusiveBetween(1, 200).WithMessage("Ask for between 1 and 200 lines at a time.");
    RuleFor(x => x.Kind).Must(k => string.IsNullOrWhiteSpace(k) || ActivityKinds.Actions.ContainsKey(k))
      .WithMessage($"The kind of change is one of: {string.Join(", ", ActivityKinds.Actions.Keys)}.");
    RuleFor(x => x.Action).Must(a => string.IsNullOrWhiteSpace(a) || Enum.TryParse<ActivityAction>(a, ignoreCase: true, out _))
      .WithMessage("That action is not one Administration records.");
  }
}
