using FluentValidation;
using Microsoft.EntityFrameworkCore;

// =====================================================
// THE OVERVIEW: what an owner must keep an eye on
// =====================================================

public sealed record OversightAccountDto(Guid Id, string DisplayName, string Username, bool IsActive, DateTime? LastLoginAt, DateTime? CreatedAt,
  IReadOnlyList<string> Roles, Guid? EmployeeId, string? EmployeeCode);

public sealed record OversightExceptionDto(Guid UserId, string UserName, string PermissionCode, string PermissionName, string Effect,
  DateTime GrantedAt, DateTime? ExpiresAt, string? Reason);

public sealed record OversightConflictDto(Guid UserId, string UserName, IReadOnlyList<string> Reasons);

public sealed record OversightRuleOffDto(string Code, string Title, string ChangedByName, DateTime ChangedAt, string Reason);

public sealed record OversightReviewDto(Guid? OpenReviewId, DateTime? OpenSince, int Decided, int ToDecide, DateTime? LastClosedAt, DateOnly DueOn, bool Overdue);

/// deletedRecoveryDays: how long a deleted account can be restored (30 or 90).
public sealed record OversightSettingsDto(int DormantAfterDays, bool SwitchOffDormant, int AccessReviewMonths, int SignInMaxDays, int DeletedRecoveryDays = 90);

/// Administration › Overview in one answer:
/// - who holds Super Administrator;
/// - accounts nobody has used for DormantAfterDays (or never used since they were made that long ago);
/// - exceptions in force; separation-of-duty rules switched off;
/// - accounts holding two duties that should not meet;
/// - the access review: open, decided so far, next due;
/// - sessions open now, failed sign-ins in 24 hours, locked accounts;
/// - the latest acts that change power (roles, exceptions, rules, settings, refused attempts, theft).
public sealed record OversightDto(
  IReadOnlyList<OversightAccountDto> SuperAdmins,
  IReadOnlyList<OversightAccountDto> Dormant,
  IReadOnlyList<OversightExceptionDto> Exceptions,
  IReadOnlyList<OversightRuleOffDto> RulesOff,
  IReadOnlyList<OversightConflictDto> Conflicts,
  OversightReviewDto Review,
  int OpenSessions,
  int FailedSignIns24h,
  int Locked,
  IReadOnlyList<AdminActivityDto> PowerChanges,
  OversightSettingsDto Settings);

public sealed record GetOversightQuery : IQuery<Result<OversightDto>>;

public class GetOversightHandler(IApplicationDbContext context, ISecuritySettingsProvider settingsProvider) : IQueryHandler<GetOversightQuery, Result<OversightDto>>
{
  /// The acts that change who can do what - listed on the Overview for 30 days.
  public static readonly ActivityAction[] PowerActions =
  [
    ActivityAction.RolesChanged, ActivityAction.RoleAssigned, ActivityAction.RoleRemoved, ActivityAction.PermissionOverrideSet,
    ActivityAction.PermissionOverrideRemoved, ActivityAction.RolePermissionsChanged, ActivityAction.RolePermissionsAdded,
    ActivityAction.RolePermissionRemoved, ActivityAction.DutyRuleSwitched, ActivityAction.SecuritySettingsUpdated,
    ActivityAction.OversightSettingsUpdated, ActivityAction.UserRestored, ActivityAction.ScopeChanged, ActivityAction.ConflictAccepted,
    ActivityAction.ActRefused, ActivityAction.SessionTheftDetected, ActivityAction.SuperAdminSeeded, ActivityAction.PermissionsSeeded,
    ActivityAction.PasswordReset, ActivityAction.DormantAccountsSwitchedOff
  ];

  public async Task<Result<OversightDto>> Handle(GetOversightQuery query, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var settings = await settingsProvider.GetAsync(cancellationToken);
    var map = await AccessMap.BuildAsync(context, cancellationToken);
    var accounts = map.Accounts.Values.Where(a => !a.User.IsDeleted).ToList();

    OversightAccountDto Dto(AccessMap.Account a) => new(a.User.Id.Value, a.User.DisplayName.Value, a.User.Username.Value, a.User.IsActive,
      a.User.LastLoginAt, a.User.CreatedAt, a.Roles.Where(r => r.Assignment.IsLive(now)).Select(r => r.Role.RoleName.Value).ToList(),
      a.User.EmployeeId, a.User.EmployeeCode?.Value);

    var superAdmins = accounts.Where(map.IsSuperAdmin).Select(Dto).ToList();
    var dormantBefore = now.AddDays(-settings.DormantAfterDays);
    var dormant = accounts.Where(a => a.User.IsActive && (a.User.LastLoginAt ?? a.User.CreatedAt ?? now) < dormantBefore)
      .OrderBy(a => a.User.LastLoginAt ?? a.User.CreatedAt).Select(Dto).ToList();

    var exceptions = await context.UserPermissionOverrides.AsNoTracking()
      .Where(o => o.ExpiresAt == null || o.ExpiresAt > now)
      .Join(context.Permissions.AsNoTracking(), o => o.PermissionId, p => p.Id, (o, p) => new { o, p })
      .Join(context.Users.AsNoTracking(), x => x.o.UserId, u => u.Id, (x, u) => new { x.o, x.p, u })
      .OrderByDescending(x => x.o.GrantedAt)
      .Select(x => new OversightExceptionDto(x.u.Id.Value, x.u.DisplayName.Value, x.p.Code.Value, x.p.PermissionName.Value, x.o.Effect.ToString(),
        x.o.GrantedAt, x.o.ExpiresAt, x.o.Reason))
      .ToListAsync(cancellationToken);

    var rulesOff = (await context.DutyRuleSwitches.AsNoTracking().Where(r => !r.IsOn).ToListAsync(cancellationToken))
      .Select(r => new OversightRuleOffDto(r.Id, DutyRules.Find(r.Id)?.Title ?? r.Id, r.ChangedByName, r.ChangedAt, r.Reason))
      .ToList();

    // a Super Administrator holds everything by design; the pairs matter for everyone else
    var conflicts = accounts.Where(a => a.User.IsActive && !map.IsSuperAdmin(a))
      .Select(a => (a, Pairs: AccessMap.ConflictsIn(a.Permissions.Keys)))
      .Where(x => x.Pairs.Count > 0)
      .Select(x => new OversightConflictDto(x.a.User.Id.Value, x.a.User.DisplayName.Value, x.Pairs.Select(p => p.Reason).ToList()))
      .ToList();

    var review = await AccessReviewState.OfAsync(context, settings, now, cancellationToken);

    var openSessions = await context.Sessions.AsNoTracking().CountAsync(s => s.RevokedAt == null && s.ExpiresAt > now, cancellationToken);
    var since = now.AddHours(-24);
    var failed = await context.LoginAttempts.AsNoTracking().CountAsync(a => !a.Succeeded && a.AttemptedAt >= since, cancellationToken);
    var locked = await context.Users.AsNoTracking().CountAsync(u => u.LockedUntil != null && u.LockedUntil > now, cancellationToken);

    var monthAgo = now.AddDays(-30);
    var power = await context.AdminActivities.AsNoTracking()
      .Where(a => a.OccurredAt >= monthAgo && PowerActions.Contains(a.Action))
      .OrderByDescending(a => a.OccurredAt)
      .Take(25)
      .ToListAsync(cancellationToken);

    return Result<OversightDto>.Success(new OversightDto(superAdmins, dormant, exceptions, rulesOff, conflicts, review, openSessions, failed, locked,
      power.Select(a => a.ToDto()).ToList(),
      new OversightSettingsDto(settings.DormantAfterDays, settings.SwitchOffDormant, settings.AccessReviewMonths, settings.SignInMaxDays, settings.DeletedRecoveryDays)));
  }
}

// =====================================================
// THE ACCESS REPORT: every account, what it can do and why
// =====================================================

public sealed record AccessGrantDto(string Code, string Name, IReadOnlyList<string> Sources);

public sealed record AccessReportRowDto(
  Guid UserId,
  string DisplayName,
  string Username,
  bool IsActive,
  bool IsLocked,
  bool IsSuperAdmin,
  DateTime? LastLoginAt,
  string? EmployeeCode,
  IReadOnlyList<string> Roles,
  IReadOnlyList<string> Towns,
  IReadOnlyList<string> Offices,
  IReadOnlyList<AccessGrantDto> Permissions,
  IReadOnlyList<string> Denied,
  IReadOnlyList<string> Conflicts);

/// Every account (deleted ones left out) with each permission it holds and where it comes from - the source of the
/// fixed access reports: who can do X, what can this person do, privileged accounts, conflicting duties.
public sealed record GetAccessReportQuery : IQuery<Result<IReadOnlyList<AccessReportRowDto>>>;

public class GetAccessReportHandler(IApplicationDbContext context) : IQueryHandler<GetAccessReportQuery, Result<IReadOnlyList<AccessReportRowDto>>>
{
  public async Task<Result<IReadOnlyList<AccessReportRowDto>>> Handle(GetAccessReportQuery query, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var map = await AccessMap.BuildAsync(context, cancellationToken);
    var rows = map.Accounts.Values
      .Where(a => !a.User.IsDeleted)
      .OrderBy(a => a.User.DisplayName.Value)
      .Select(a => new AccessReportRowDto(
        a.User.Id.Value, a.User.DisplayName.Value, a.User.Username.Value, a.User.IsActive, a.User.IsLockedOut(now), map.IsSuperAdmin(a),
        a.User.LastLoginAt, a.User.EmployeeCode?.Value,
        a.Roles.Where(r => r.Assignment.IsLive(now)).Select(r => r.Role.RoleName.Value).OrderBy(n => n).ToList(),
        a.Scopes.Where(s => s.Kind == ScopeKind.Town).Select(s => s.Label).OrderBy(l => l).ToList(),
        a.Scopes.Where(s => s.Kind == ScopeKind.Office).Select(s => s.Label).OrderBy(l => l).ToList(),
        a.Permissions.Values.OrderBy(g => g.Code, StringComparer.Ordinal)
          .Select(g => new AccessGrantDto(g.Code, map.Names.GetValueOrDefault(g.Code) ?? g.Code, g.Sources)).ToList(),
        a.Denied.OrderBy(c => c, StringComparer.Ordinal).ToList(),
        map.IsSuperAdmin(a) ? [] : AccessMap.ConflictsIn(a.Permissions.Keys).Select(c => c.Reason).ToList()))
      .ToList();
    return Result<IReadOnlyList<AccessReportRowDto>>.Success(rows);
  }
}

// =====================================================
// SESSIONS OPEN NOW, across every account
// =====================================================

public sealed record OpenSessionDto(Guid SessionId, Guid UserId, string DisplayName, string Username, string? IpAddress, string? Device,
  DateTime SignedInAt, DateTime LastRenewedAt, DateTime ExpiresAt);

public sealed record GetOpenSessionsQuery : IQuery<Result<IReadOnlyList<OpenSessionDto>>>;

public class GetOpenSessionsHandler(IApplicationDbContext context) : IQueryHandler<GetOpenSessionsQuery, Result<IReadOnlyList<OpenSessionDto>>>
{
  public async Task<Result<IReadOnlyList<OpenSessionDto>>> Handle(GetOpenSessionsQuery query, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var rows = await context.Sessions.AsNoTracking()
      .Where(s => s.RevokedAt == null && s.ExpiresAt > now)
      .Join(context.Users.AsNoTracking(), s => s.UserId, u => u.Id, (s, u) => new { s, u })
      .OrderByDescending(x => x.s.IssuedAt)
      .Take(500)
      .ToListAsync(cancellationToken);
    return Result<IReadOnlyList<OpenSessionDto>>.Success(rows.Select(x => new OpenSessionDto(x.s.Id.Value, x.u.Id.Value, x.u.DisplayName.Value,
      x.u.Username.Value, x.s.IpAddress?.Value, x.s.UserAgent, x.s.SignedInAt, x.s.IssuedAt, x.s.ExpiresAt)).ToList());
  }
}

/// Signs one session out (one device of one person) - the same guard rails as signing a person out everywhere.
public sealed record RevokeSessionCommand(Guid SessionId) : ICommand<Result<bool>>;

public class RevokeSessionHandler(IApplicationDbContext context, IdentityGuard guard, IActivityRecorder activity) : ICommandHandler<RevokeSessionCommand, Result<bool>>
{
  public async Task<Result<bool>> Handle(RevokeSessionCommand command, CancellationToken cancellationToken)
  {
    var session = await context.Sessions.FirstOrDefaultAsync(s => s.Id == SessionId.Of(command.SessionId), cancellationToken)
      ?? throw new DomainException("This session was not found.");
    if (session.IsRevoked)
      return Result<bool>.Failure("This session has already ended.");
    await guard.EnsureCanActOnAsync(session.UserId, "sign out", cancellationToken);
    session.Revoke(DateTime.UtcNow, "Signed out by an administrator");
    await activity.RecordAsync(ActivityAction.SessionsRevoked, ActivityTargetType.User, session.UserId.Value, null,
      $"One session · {session.IpAddress?.Value ?? "unknown address"}", cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return Result<bool>.Success(true);
  }
}

// =====================================================
// OVERSIGHT SETTINGS
// =====================================================

public sealed record UpdateOversightSettingsCommand(OversightSettingsDto Settings) : ICommand<Result<OversightSettingsDto>>;

public class UpdateOversightSettingsHandler(IApplicationDbContext context, IActivityRecorder activity) : ICommandHandler<UpdateOversightSettingsCommand, Result<OversightSettingsDto>>
{
  public async Task<Result<OversightSettingsDto>> Handle(UpdateOversightSettingsCommand command, CancellationToken cancellationToken)
  {
    var id = SecuritySettingsId.Of(SecuritySettings.SingletonId);
    var settings = await context.SecuritySettings.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
      ?? throw new DomainException("The security settings are not set up yet - restart the Identity service.");
    var before = new OversightSettingsDto(settings.DormantAfterDays, settings.SwitchOffDormant, settings.AccessReviewMonths, settings.SignInMaxDays, settings.DeletedRecoveryDays);
    var s = command.Settings;
    settings.UpdateOversight(s.DormantAfterDays, s.SwitchOffDormant, s.AccessReviewMonths, s.SignInMaxDays, s.DeletedRecoveryDays);
    var detail = ActivityDetail.Changes(
      ("Dormant after (days)", before.DormantAfterDays.ToString(), s.DormantAfterDays.ToString()),
      ("Switch dormant accounts off", ActivityDetail.YesNo(before.SwitchOffDormant), ActivityDetail.YesNo(s.SwitchOffDormant)),
      ("Access review every (months)", before.AccessReviewMonths.ToString(), s.AccessReviewMonths.ToString()),
      ("Longest sign-in (days)", before.SignInMaxDays.ToString(), s.SignInMaxDays.ToString()),
      ("Deleted accounts can be restored for (days)", before.DeletedRecoveryDays.ToString(), s.DeletedRecoveryDays.ToString()));
    if (detail is not null)
    {
      await activity.RecordAsync(ActivityAction.OversightSettingsUpdated, ActivityTargetType.System, null, "Oversight settings", detail, cancellationToken);
      await context.SaveChangesAsync(cancellationToken);
    }
    return Result<OversightSettingsDto>.Success(s);
  }
}

// =====================================================
// SEPARATION OF DUTIES WHEN ACCESS IS GIVEN
// =====================================================

public sealed record PermissionConflictDto(string First, string Second, string Reason);

public static class ConflictCheck
{
  /// Refuses a save that puts two duties together without a reason; with a reason, writes it to the trail. `codes` is
  /// what the role (or the person) will hold after the save.
  public static async Task EnsureAcceptedAsync(IActivityRecorder activity, IEnumerable<string> codes, string? reason, ActivityTargetType targetType,
      Guid targetId, string targetLabel, CancellationToken cancellationToken)
  {
    var pairs = AccessMap.ConflictsIn(codes);
    if (pairs.Count == 0)
      return;
    if (string.IsNullOrWhiteSpace(reason))
      throw new DomainException($"CONFLICT: this puts together duties that should be kept apart - {string.Join(" ", pairs.Select(p => p.Reason))} Say why it is needed.");
    await activity.RecordAsync(ActivityAction.ConflictAccepted, targetType, targetId, targetLabel,
      $"{string.Join("; ", pairs.Select(p => $"{p.First} + {p.Second}"))} · {reason.Trim()}", cancellationToken);
  }
}
