using FluentValidation;
using Microsoft.EntityFrameworkCore;

// Announcements: HR posts a notice for everyone or for one unit and the units under it; the portal shows the ones
// running today to the people they are for, and notes who opened them.

public sealed record AnnouncementInput(string Title, string Body, Guid? OrgUnitId, DateOnly ShowFrom, DateOnly? ShowUntil, bool IsPinned);

public sealed record AnnouncementDto(
  Guid Id,
  string Title,
  string Body,
  Guid? OrgUnitId,
  /// The unit it is for (with the units under it); null = everyone.
  string? OrgUnit,
  DateOnly ShowFrom,
  DateOnly? ShowUntil,
  bool IsPinned,
  string? FileName,
  bool IsShowing,
  bool IsWithdrawn,
  DateTime? WithdrawnAt,
  string? WithdrawnReason,
  DateTime? PostedAt,
  string? PostedBy,
  /// HR: how many of the people it is for have opened it, and how many it is for (in service today).
  int ReadCount,
  int AudienceCount,
  /// The portal: whether I have opened it.
  bool IsRead);

public sealed record GetAnnouncementsQueryResult(IReadOnlyList<AnnouncementDto> Announcements);

/// HR: every announcement, newest first (withdrawn ones too when asked).
public sealed record GetAnnouncementsQuery(bool IncludeWithdrawn) : IQuery<Result<GetAnnouncementsQueryResult>>;
/// The portal: the ones running today for the employee, pinned first, then newest.
public sealed record GetMyAnnouncementsQuery(Guid EmployeeId) : IQuery<Result<GetAnnouncementsQueryResult>>;
public sealed record GetAnnouncementFileQuery(Guid Id, Guid? ForEmployee, bool Download) : IQuery<Result<StoredFile>>;

public sealed record PostAnnouncementCommand(AnnouncementInput Announcement) : ICommand<Result<CreatedResult>>;
public sealed record EditAnnouncementCommand(Guid Id, AnnouncementInput Announcement) : ICommand<Result<UpdatedResult>>;
public sealed record WithdrawAnnouncementCommand(Guid Id, string? Reason) : ICommand<Result<UpdatedResult>>;
public sealed record AttachAnnouncementFileCommand(Guid Id, UploadedFile File) : ICommand<Result<UpdatedResult>>;
public sealed record MarkAnnouncementReadCommand(Guid Id, Guid EmployeeId) : ICommand<Result<UpdatedResult>>;

public class AnnouncementInputValidator : AbstractValidator<AnnouncementInput>
{
  public AnnouncementInputValidator()
  {
    RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
    RuleFor(x => x.Body).NotEmpty().WithMessage("Write the message.").MaximumLength(4000);
    RuleFor(x => x.ShowUntil).GreaterThanOrEqualTo(x => x.ShowFrom).When(x => x.ShowUntil is not null).WithMessage("It cannot stop showing before it starts.");
  }
}

public class PostAnnouncementCommandValidator : AbstractValidator<PostAnnouncementCommand>
{
  public PostAnnouncementCommandValidator() => RuleFor(x => x.Announcement).NotNull().SetValidator(new AnnouncementInputValidator());
}

public class EditAnnouncementCommandValidator : AbstractValidator<EditAnnouncementCommand>
{
  public EditAnnouncementCommandValidator() => RuleFor(x => x.Announcement).NotNull().SetValidator(new AnnouncementInputValidator());
}

public class AnnouncementHandlers(IApplicationDbContext context, FileUploads files, IClock clock, ICurrentUser currentUser, EmployeeScope scope, IAuditTrail audit) :
  IQueryHandler<GetAnnouncementsQuery, Result<GetAnnouncementsQueryResult>>,
  IQueryHandler<GetMyAnnouncementsQuery, Result<GetAnnouncementsQueryResult>>,
  IQueryHandler<GetAnnouncementFileQuery, Result<StoredFile>>,
  ICommandHandler<PostAnnouncementCommand, Result<CreatedResult>>,
  ICommandHandler<EditAnnouncementCommand, Result<UpdatedResult>>,
  ICommandHandler<WithdrawAnnouncementCommand, Result<UpdatedResult>>,
  ICommandHandler<AttachAnnouncementFileCommand, Result<UpdatedResult>>,
  ICommandHandler<MarkAnnouncementReadCommand, Result<UpdatedResult>>
{
  public async Task<Result<GetAnnouncementsQueryResult>> Handle(GetAnnouncementsQuery query, CancellationToken cancellationToken)
  {
    var rows = await context.Announcements.AsNoTracking().Where(a => query.IncludeWithdrawn || !a.IsWithdrawn)
      .OrderByDescending(a => a.ShowFrom).ThenByDescending(a => a.CreatedAt).ToListAsync(cancellationToken);
    var tree = await UnitTreeAsync(cancellationToken);
    var placed = await PlacedAsync(cancellationToken);
    var everyone = await InServiceAsync(cancellationToken);
    var ids = rows.Select(a => a.Id).ToList();
    List<(AnnouncementId Announcement, EmployeeId Employee)> reads;
    using (scope.Unrestricted())
      reads = (await context.AnnouncementReads.AsNoTracking().Where(r => ids.Contains(r.AnnouncementId))
        .Select(r => new { r.AnnouncementId, r.EmployeeId }).ToListAsync(cancellationToken)).Select(r => (r.AnnouncementId, r.EmployeeId)).ToList();
    var posters = await PostersAsync(rows, cancellationToken);
    var today = clock.Today;

    return Result<GetAnnouncementsQueryResult>.Success(new(rows.Select(a =>
    {
      // everyone = everyone in service (with a post or not); a unit = the people whose post sits in it or under it
      var audience = a.OrgUnitId is { } unit ? placed.Where(p => Under(tree, p.Unit, unit.Value)).Select(p => p.Employee).ToHashSet() : everyone;
      var read = reads.Count(r => r.Announcement == a.Id && audience.Contains(r.Employee));
      return Dto(a, tree, posters, today, read, audience.Count, false);
    }).ToList()));
  }

  public async Task<Result<GetAnnouncementsQueryResult>> Handle(GetMyAnnouncementsQuery query, CancellationToken cancellationToken)
  {
    var me = EmployeeId.Of(query.EmployeeId);
    var today = clock.Today;
    var tree = await UnitTreeAsync(cancellationToken);
    var unit = await UnitOfAsync(me, cancellationToken);
    var rows = (await context.Announcements.AsNoTracking()
        .Where(a => !a.IsWithdrawn && a.ShowFrom <= today && (a.ShowUntil == null || a.ShowUntil >= today)).ToListAsync(cancellationToken))
      .Where(a => a.OrgUnitId is not { } target || (unit is { } mine && Under(tree, mine, target.Value)))
      .OrderByDescending(a => a.IsPinned).ThenByDescending(a => a.ShowFrom).ThenByDescending(a => a.CreatedAt).ToList();
    var ids = rows.Select(a => a.Id).ToList();
    var read = (await context.AnnouncementReads.AsNoTracking().Where(r => r.EmployeeId == me && ids.Contains(r.AnnouncementId))
      .Select(r => r.AnnouncementId).ToListAsync(cancellationToken)).ToHashSet();
    var posters = await PostersAsync(rows, cancellationToken);
    return Result<GetAnnouncementsQueryResult>.Success(new(rows.Select(a => Dto(a, tree, posters, today, 0, 0, read.Contains(a.Id))).ToList()));
  }

  public async Task<Result<StoredFile>> Handle(GetAnnouncementFileQuery query, CancellationToken cancellationToken)
  {
    var announcement = await LoadAsync(query.Id, cancellationToken);
    if (query.ForEmployee is { } employee)
      await EnsureForAsync(announcement, EmployeeId.Of(employee), cancellationToken);
    if (announcement.FilePath is not { } path)
      throw new StoredFileNotFoundException("This announcement has no paper.");
    audit.Record(query.Download ? AuditActions.Downloaded : AuditActions.Viewed, announcement, announcement.FileName);
    await audit.SaveAsync(cancellationToken);
    return Result<StoredFile>.Success(await files.OpenAsync(path, Path.GetFileNameWithoutExtension(announcement.FileName ?? "notice"), cancellationToken));
  }

  public async Task<Result<CreatedResult>> Handle(PostAnnouncementCommand command, CancellationToken cancellationToken)
  {
    var i = command.Announcement;
    var unit = await UnitIdAsync(i.OrgUnitId, cancellationToken);
    var announcement = Announcement.Post(AnnouncementId.New(), i.Title, i.Body, unit, i.ShowFrom, i.ShowUntil, i.IsPinned);
    context.Announcements.Add(announcement);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(announcement.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(EditAnnouncementCommand command, CancellationToken cancellationToken)
  {
    var announcement = await LoadAsync(command.Id, cancellationToken);
    var i = command.Announcement;
    announcement.Edit(i.Title, i.Body, await UnitIdAsync(i.OrgUnitId, cancellationToken), i.ShowFrom, i.ShowUntil, i.IsPinned);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(WithdrawAnnouncementCommand command, CancellationToken cancellationToken)
  {
    var announcement = await LoadAsync(command.Id, cancellationToken);
    announcement.Withdraw(currentUser.UserId, clock.UtcNow, command.Reason);
    audit.Record(HrmVerbs.Cancelled, announcement, $"Withdrawn: {command.Reason?.Trim()}");
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(AttachAnnouncementFileCommand command, CancellationToken cancellationToken)
  {
    var announcement = await LoadAsync(command.Id, cancellationToken);
    var path = await files.SaveDocumentAsync(command.File, "announcements", "papers", cancellationToken);
    announcement.Attach(path, Path.GetFileName(command.File.FileName));
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(MarkAnnouncementReadCommand command, CancellationToken cancellationToken)
  {
    var announcement = await LoadAsync(command.Id, cancellationToken);
    var me = EmployeeId.Of(command.EmployeeId);
    await EnsureForAsync(announcement, me, cancellationToken);
    if (!await context.AnnouncementReads.AnyAsync(r => r.AnnouncementId == announcement.Id && r.EmployeeId == me, cancellationToken))
    {
      context.AnnouncementReads.Add(AnnouncementRead.Of(announcement.Id, me, clock.UtcNow));
      await context.SaveChangesAsync(cancellationToken);
    }
    return CommandResults.Updated();
  }

  // ---- who it is for ----

  /// Each unit's parent, today.
  private async Task<Dictionary<Guid, (Guid? Parent, string Name)>> UnitTreeAsync(CancellationToken cancellationToken) =>
      await context.CurrentOrganizationUnits.AsNoTracking().ToDictionaryAsync(u => u.OrgUnitId, u => (u.ParentUnitId, u.Name), cancellationToken);

  /// Whether `unit` is `target` or one of the units under it.
  private static bool Under(Dictionary<Guid, (Guid? Parent, string Name)> tree, Guid unit, Guid target)
  {
    Guid? at = unit;
    // the structure is a tree; the hop limit only guards against a loop in bad data
    for (var hops = 0; at is { } current && hops < 64; hops++)
    {
      if (current == target)
        return true;
      at = tree.TryGetValue(current, out var u) ? u.Parent : null;
    }
    return false;
  }

  /// The unit of the employee's post today (regular first).
  private async Task<Guid?> UnitOfAsync(EmployeeId employee, CancellationToken cancellationToken)
  {
    var today = clock.Today;
    using (scope.Unrestricted())
      return await (
          from a in context.PositionAssignments
          where a.EmployeeId == employee && a.Status == RecordStatus.Active && a.EffectiveFrom <= today && (a.EffectiveTo == null || a.EffectiveTo >= today)
          join v in context.PostVersions on a.PostId equals v.PostId
          where v.EffectiveFrom <= today && (v.EffectiveTo == null || v.EffectiveTo >= today)
          orderby a.AssignmentType
          select (Guid?)v.OrgUnitId.Value)
        .FirstOrDefaultAsync(cancellationToken);
  }

  /// Everyone in service with a post today, and its unit.
  private async Task<List<(EmployeeId Employee, Guid Unit)>> PlacedAsync(CancellationToken cancellationToken)
  {
    var today = clock.Today;
    var inService = EmploymentStatuses.InService;
    using (scope.Unrestricted())
      return (await (
          from a in context.PositionAssignments
          where a.Status == RecordStatus.Active && a.EffectiveFrom <= today && (a.EffectiveTo == null || a.EffectiveTo >= today)
          join v in context.PostVersions on a.PostId equals v.PostId
          where v.EffectiveFrom <= today && (v.EffectiveTo == null || v.EffectiveTo >= today)
          join e in context.Employees on a.EmployeeId equals e.Id
          where e.ProfileStatus == RecordStatus.Active && inService.Contains(e.EmploymentStatus)
          select new { a.EmployeeId, v.OrgUnitId })
        .ToListAsync(cancellationToken)).DistinctBy(p => p.EmployeeId).Select(p => (p.EmployeeId, p.OrgUnitId.Value)).ToList();
  }

  private async Task<HashSet<EmployeeId>> InServiceAsync(CancellationToken cancellationToken)
  {
    var inService = EmploymentStatuses.InService;
    using (scope.Unrestricted())
      return (await context.Employees.AsNoTracking().Where(e => e.ProfileStatus == RecordStatus.Active && inService.Contains(e.EmploymentStatus))
        .Select(e => e.Id).ToListAsync(cancellationToken)).ToHashSet();
  }

  private async Task EnsureForAsync(Announcement announcement, EmployeeId employee, CancellationToken cancellationToken)
  {
    var notFound = new AnnouncementNotFoundException($"Announcement {announcement.Id.Value} was not found.");
    if (!announcement.ShowsOn(clock.Today))
      throw notFound;
    if (announcement.OrgUnitId is { } target && (await UnitOfAsync(employee, cancellationToken) is not { } unit || !Under(await UnitTreeAsync(cancellationToken), unit, target.Value)))
      throw notFound;
  }

  private async Task<OrganizationUnitId?> UnitIdAsync(Guid? id, CancellationToken cancellationToken)
  {
    if (id is not { } value)
      return null;
    return (await context.LoadOrgUnitAsync(value, cancellationToken)).Id;
  }

  private async Task<Announcement> LoadAsync(Guid id, CancellationToken cancellationToken)
  {
    var key = AnnouncementId.Of(id);
    return await context.Announcements.FirstOrDefaultAsync(a => a.Id == key, cancellationToken)
      ?? throw new AnnouncementNotFoundException($"Announcement {id} was not found.");
  }

  private async Task<Dictionary<Guid, string>> PostersAsync(List<Announcement> rows, CancellationToken cancellationToken)
  {
    var ids = rows.Where(a => a.CreatedBy is not null).Select(a => a.CreatedBy).Distinct().ToList();
    using (scope.Unrestricted())
      return await context.Employees.AsNoTracking().Where(e => e.UserId != null && ids.Contains(e.UserId))
        .ToDictionaryAsync(e => e.UserId!.Value, e => e.FullName ?? e.EmployeeNumber, cancellationToken);
  }

  private static AnnouncementDto Dto(Announcement a, Dictionary<Guid, (Guid? Parent, string Name)> tree, Dictionary<Guid, string> posters, DateOnly today,
      int readCount, int audienceCount, bool isRead) =>
    new(a.Id.Value, a.Title, a.Body, a.OrgUnitId?.Value, a.OrgUnitId is { } u && tree.TryGetValue(u.Value, out var unit) ? unit.Name : null,
      a.ShowFrom, a.ShowUntil, a.IsPinned, a.FileName, a.ShowsOn(today), a.IsWithdrawn, a.WithdrawnAt, a.WithdrawnReason, a.CreatedAt,
      a.CreatedBy is { } by ? posters.GetValueOrDefault(by) : null, readCount, audienceCount, isRead);
}
