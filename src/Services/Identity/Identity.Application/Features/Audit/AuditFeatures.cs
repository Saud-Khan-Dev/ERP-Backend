using Microsoft.EntityFrameworkCore;

/// Administration's own trail in the shape every module answers `GET /audit` with, so the Activity screen lists all
/// modules in one diary. A line is one administrative act (Identity keeps sentences, not field rows): the action is the
/// act in words ("Roles changed"), the record is the account, role or system setting it was about, Detail says what changed.
public sealed record GetAdministrationAuditQuery(AuditFilter Filter) : IQuery<Result<AuditPageDto>>;

public class GetAdministrationAuditHandler(IApplicationDbContext context) : IQueryHandler<GetAdministrationAuditQuery, Result<AuditPageDto>>
{
  public const string Module = "Administration";
  private static readonly TimeSpan Pkt = TimeSpan.FromHours(5);

  public async Task<Result<AuditPageDto>> Handle(GetAdministrationAuditQuery query, CancellationToken cancellationToken)
  {
    var f = query.Filter;
    var lines = context.AdminActivities.AsNoTracking();
    if (f.From is { } from)
      lines = lines.Where(a => a.OccurredAt >= new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), Pkt).UtcDateTime);
    if (f.To is { } to)
      lines = lines.Where(a => a.OccurredAt < new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), Pkt).UtcDateTime);
    if (f.ActorId is { } actor)
      lines = lines.Where(a => a.ActorUserId == UserId.Of(actor));
    if (!string.IsNullOrWhiteSpace(f.Action))
    {
      var actions = Enum.GetValues<ActivityAction>().Where(a => ActionWords(a) == f.Action || a.ToString() == f.Action).ToList();
      lines = lines.Where(a => actions.Contains(a.Action));
    }
    if (!string.IsNullOrWhiteSpace(f.Kind))
    {
      var kinds = Enum.GetValues<ActivityTargetType>().Where(k => KindWords(k) == f.Kind).ToList();
      lines = lines.Where(a => kinds.Contains(a.TargetType));
    }
    if ((f.RecordId ?? f.ParentId) is { } record)
      lines = lines.Where(a => a.TargetId == record);
    if (!string.IsNullOrWhiteSpace(f.Search))
    {
      var text = f.Search.Trim().ToLower();
      lines = lines.Where(a => a.ActorName.ToLower().Contains(text) || (a.TargetLabel != null && a.TargetLabel.ToLower().Contains(text))
        || (a.Detail != null && a.Detail.ToLower().Contains(text)));
    }

    var count = await lines.LongCountAsync(cancellationToken);
    if (f.Before is { } before)
      lines = lines.Where(a => a.OccurredAt < before.UtcDateTime);
    var size = Math.Clamp(f.PageSize ?? 50, 1, 200);
    var rows = await lines.OrderByDescending(a => a.OccurredAt).Take(size).ToListAsync(cancellationToken);
    return Result<AuditPageDto>.Success(new AuditPageDto(rows.Select(ToEntry).ToList(), count));
  }

  public static AuditEntryDto ToEntry(AdminActivity a) => new(
    a.Id.Value, new DateTimeOffset(DateTime.SpecifyKind(a.OccurredAt, DateTimeKind.Utc)), Module, a.ActorUserId?.Value, a.ActorName,
    ActionWords(a.Action), KindWords(a.TargetType), a.TargetId, null, a.TargetLabel, a.TargetId, null, null, null, null, a.Detail,
    a.IpAddress?.Value, a.UserAgent, null);

  public static string ActionWords(ActivityAction action) => AuditProfile.Words(action.ToString());

  public static string KindWords(ActivityTargetType kind) => kind switch
  {
    ActivityTargetType.User => "Account",
    ActivityTargetType.Role => "Role",
    _ => "System setting"
  };
}

/// An export or a print of an Administration register (accounts, the audit trail, an access report), noted in the trail.
public sealed record RecordAdministrationExportCommand(RecordExportRequest Export) : ICommand<Result<bool>>;

public class RecordAdministrationExportHandler(IApplicationDbContext context, IActivityRecorder activity) : ICommandHandler<RecordAdministrationExportCommand, Result<bool>>
{
  public async Task<Result<bool>> Handle(RecordAdministrationExportCommand command, CancellationToken cancellationToken)
  {
    var e = command.Export;
    if (string.IsNullOrWhiteSpace(e.What))
      return Result<bool>.Failure("Say what was exported.");
    var detail = string.Join(" · ", new[] { $"{e.Rows} row{(e.Rows == 1 ? "" : "s")}", e.Format == "print" ? "printed" : null, e.Filters }
      .Where(x => !string.IsNullOrWhiteSpace(x)));
    await activity.RecordAsync(ActivityAction.Exported, ActivityTargetType.System, e.RecordId, e.What.Trim(), detail, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return Result<bool>.Success(true);
  }
}
