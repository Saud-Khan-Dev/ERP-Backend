using FluentValidation;
using Microsoft.EntityFrameworkCore;

public sealed record HrActionDto(
  Guid Id,
  HrActionType ActionType,
  Guid EmployeeId,
  string EmployeeNumber,
  string EmployeeName,
  Guid? OldPostId,
  string? OldPostCode,
  Guid? NewPostId,
  string? NewPostCode,
  int? OldBps,
  int? NewBps,
  string? OldOrgUnit,
  string? NewOrgUnit,
  DateOnly EffectiveDate,
  string? OrderNumber,
  string? Reason,
  Guid? SupportingDocumentId,
  HrActionStatus Status,
  Guid? ApprovalRequestId,
  Guid? ApprovedBy,
  DateTime? ApprovedAt,
  Guid? ResultingServiceHistoryId,
  Guid? CreatedBy,
  DateTime? CreatedAt,
  DateTime? UpdatedAt,
  DateOnly? EndDate,
  Guid? EventTypeId,
  string? EventType,
  string? ExternalReferenceOrg,
  Guid? EmploymentMethodId,
  Guid? RecruitmentMethodId,
  int? StageNumber,
  decimal? BasicPay,
  int? NoticePeriodDays,
  Guid? SubmittedBy,
  DateTime? SubmittedAt,
  Guid? DecidedBy,
  DateTime? DecidedAt,
  string? DecisionRemarks,
  Guid? AppliedBy,
  DateTime? AppliedAt,
  Guid? LeaveApplicationId,
  /// The action changes an undated fact (status, employment type): it is applied on or after its date.
  bool TakesEffectOnItsDate,
  /// Approved, and its date has not come yet ("Approved - takes effect on ...").
  bool WaitsForDate);

/// The draft of an HR action. Everything applying it needs is given here (the approver approves the whole act):
/// EndDate = the last day of leave without pay (or of a deputation out), EventTypeId = a service event added in
/// Configuration (ActionType Other), ExternalReferenceOrg = the other department of a deputation, EmploymentMethod =
/// regularization, RecruitmentMethodId / StageNumber / BasicPay = appointment, NoticePeriodDays = resignation.
public sealed record HrActionInput(
  HrActionType ActionType,
  Guid? NewPostId,
  DateOnly EffectiveDate,
  string? OrderNumber,
  string? Reason,
  Guid? SupportingDocumentId,
  DateOnly? EndDate = null,
  Guid? EventTypeId = null,
  string? ExternalReferenceOrg = null,
  Guid? EmploymentMethodId = null,
  Guid? RecruitmentMethodId = null,
  int? StageNumber = null,
  decimal? BasicPay = null,
  int? NoticePeriodDays = null);

/// The desk's views of the queue: mine = drafted or submitted by me; toApprove = pending, drafted by someone else when
/// maker-checker is on; toApply = approved and due (its date has come, or a dated act); waiting = approved, taking effect
/// on a later date.
public enum HrActionView
{
  Mine,
  ToApprove,
  ToApply,
  Waiting
}

public sealed record GetHrActionsQueryResult(PaginatedResult<HrActionDto> HrActions);

/// Search = order number, employee number or name. OrgUnitId = the old or the new post's unit (with its sub-units).
public sealed record GetHrActionsQuery(
  PaginationRequest Pagination,
  HrActionStatus? Status,
  HrActionType? ActionType,
  Guid? EmployeeId,
  DateOnly? From,
  DateOnly? To,
  string? Search = null,
  Guid? OrgUnitId = null,
  HrActionView? View = null,
  bool OldestFirst = false) : IQuery<Result<GetHrActionsQueryResult>>;

public sealed record GetHrActionQueryResult(HrActionDto HrAction);

public sealed record GetHrActionQuery(Guid Id) : IQuery<Result<GetHrActionQueryResult>>;

/// Drafts an HR action. The employee's current post (on the effective date) is captured as the "old" side; the new
/// grade and unit come from the new post. The rules are checked now, not only when it is applied.
public sealed record CreateHrActionCommand(Guid EmployeeId, HrActionInput Action) : ICommand<Result<CreatedResult>>;

public sealed record UpdateHrActionCommand(Guid Id, HrActionInput Action) : ICommand<Result<UpdatedResult>>;

public enum HrActionStep
{
  Submit,
  Return,
  Approve,
  Reject,
  Cancel
}

/// Remarks: why it is returned, rejected (required) or cancelled.
public sealed record MoveHrActionCommand(Guid Id, HrActionStep Step, Guid? ApprovalRequestId, string? Remarks = null) : ICommand<Result<UpdatedResult>>;

public sealed record ApplyHrActionResult(Guid Id, SeparationChecklistDto? Checklist, IReadOnlyList<PayrollImpactDto> PayrollImpact);

/// Carries out an approved action (see HrActionApplier).
public sealed record ApplyHrActionCommand(Guid Id, HrActionApplyInput Input) : ICommand<Result<ApplyHrActionResult>>;

// ---- what the dialog offers ----

public sealed record HeldPostDto(Guid PostId, string PostCode, string? Designation, string? OrgUnit, int Bps, DateOnly Since);

/// Whether an action kind fits the person on the date; PostRule = higher / lower / same / any when it needs a post.
public sealed record HrActionOptionDto(HrActionType ActionType, bool Available, string? Reason, bool NeedsPost, string? PostRule, bool TakesEffectOnItsDate);

public sealed record HrActionEventOptionDto(Guid EventTypeId, string Name, string? Category);

public sealed record HrActionOptionsDto(
  Guid EmployeeId,
  DateOnly Date,
  EmploymentStatus EmploymentStatus,
  EmploymentBasis EmploymentBasis,
  HeldPostDto? Current,
  CurrentPayDto? Pay,
  Guid? LienPostId,
  string? LienPostCode,
  bool RequireSeparateApprover,
  IReadOnlyList<HrActionOptionDto> Actions,
  IReadOnlyList<HrActionEventOptionDto> Events);

public sealed record GetHrActionOptionsQuery(Guid EmployeeId, DateOnly? Date) : IQuery<Result<HrActionOptionsDto>>;

/// A post the action could move the employee to, with why it cannot when it cannot.
public sealed record HrActionPostDto(Guid PostId, string PostCode, string? Designation, string? OrgUnit, int Bps, int Seats, int SeatsTaken, bool Available, string? Reason);

public sealed record GetHrActionPostsQuery(Guid EmployeeId, HrActionType ActionType, DateOnly Date, string? Search) : IQuery<Result<IReadOnlyList<HrActionPostDto>>>;

/// What the action would do: Problem = why it cannot (null when it can); Consequence = one plain line.
public sealed record HrActionPreviewDto(
  bool Ok,
  string? Problem,
  string? Consequence,
  int? BpsBefore,
  int? BpsAfter,
  decimal? PayBefore,
  decimal? PayAfter,
  int? StageAfter,
  DateOnly From,
  EmploymentStatus? StatusAfter);

public sealed record PreviewHrActionQuery(Guid EmployeeId, HrActionInput Action) : IQuery<Result<HrActionPreviewDto>>;

public class HrActionInputValidator : AbstractValidator<HrActionInput>
{
  public HrActionInputValidator()
  {
    RuleFor(x => x.ActionType).IsInEnum();
    RuleFor(x => x.OrderNumber).MaximumLength(100);
    RuleFor(x => x.Reason).MaximumLength(4000);
    RuleFor(x => x.ExternalReferenceOrg).MaximumLength(200);
    RuleFor(x => x.EmploymentMethodId).NotEqual(Guid.Empty).When(x => x.EmploymentMethodId is not null);
    RuleFor(x => x.StageNumber).InclusiveBetween(0, 100).When(x => x.StageNumber is not null);
    RuleFor(x => x.BasicPay).GreaterThanOrEqualTo(0).When(x => x.BasicPay is not null);
    RuleFor(x => x.NoticePeriodDays).GreaterThanOrEqualTo(0).When(x => x.NoticePeriodDays is not null);
    RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.EffectiveDate).When(x => x.EndDate is not null)
      .WithMessage("The last day cannot be before the first.");
  }
}

public class CreateHrActionCommandValidator : AbstractValidator<CreateHrActionCommand>
{
  public CreateHrActionCommandValidator()
  {
    RuleFor(x => x.EmployeeId).NotEmpty();
    RuleFor(x => x.Action).NotNull().SetValidator(new HrActionInputValidator());
  }
}

public class UpdateHrActionCommandValidator : AbstractValidator<UpdateHrActionCommand>
{
  public UpdateHrActionCommandValidator() => RuleFor(x => x.Action).NotNull().SetValidator(new HrActionInputValidator());
}

public class MoveHrActionCommandValidator : AbstractValidator<MoveHrActionCommand>
{
  public MoveHrActionCommandValidator()
  {
    RuleFor(x => x.Step).IsInEnum();
    RuleFor(x => x.Remarks).MaximumLength(1000);
    RuleFor(x => x.Remarks).NotEmpty().When(x => x.Step == HrActionStep.Reject).WithMessage("Give the reason for rejecting the HR action.");
  }
}

public class ApplyHrActionCommandValidator : AbstractValidator<ApplyHrActionCommand>
{
  public ApplyHrActionCommandValidator()
  {
    RuleFor(x => x.Input).NotNull();
    RuleFor(x => x.Input.ExternalReferenceOrg).MaximumLength(200);
    RuleFor(x => x.Input.EmploymentMethodId).NotEqual(Guid.Empty).When(x => x.Input?.EmploymentMethodId is not null);
    RuleFor(x => x.Input.StageNumber).GreaterThanOrEqualTo(0).When(x => x.Input?.StageNumber is not null);
    RuleFor(x => x.Input.NoticePeriodDays).GreaterThanOrEqualTo(0).When(x => x.Input?.NoticePeriodDays is not null);
    RuleFor(x => x.Input.Remarks).MaximumLength(4000);
  }
}

public class PreviewHrActionQueryValidator : AbstractValidator<PreviewHrActionQuery>
{
  public PreviewHrActionQueryValidator() => RuleFor(x => x.Action).NotNull().SetValidator(new HrActionInputValidator());
}

public class HrActionHandlers(
  IApplicationDbContext context,
  ServiceRecordReader reader,
  HrActionApplier applier,
  HrActionChecks checks,
  PayService pay,
  HrmSettings settings,
  HrLookup lookup,
  ICurrentUser currentUser,
  IClock clock,
  DutyGuard duty,
  IAuditTrail audit) :
  IQueryHandler<GetHrActionsQuery, Result<GetHrActionsQueryResult>>,
  IQueryHandler<GetHrActionQuery, Result<GetHrActionQueryResult>>,
  IQueryHandler<GetHrActionOptionsQuery, Result<HrActionOptionsDto>>,
  IQueryHandler<GetHrActionPostsQuery, Result<IReadOnlyList<HrActionPostDto>>>,
  IQueryHandler<PreviewHrActionQuery, Result<HrActionPreviewDto>>,
  ICommandHandler<CreateHrActionCommand, Result<CreatedResult>>,
  ICommandHandler<UpdateHrActionCommand, Result<UpdatedResult>>,
  ICommandHandler<MoveHrActionCommand, Result<UpdatedResult>>,
  ICommandHandler<ApplyHrActionCommand, Result<ApplyHrActionResult>>
{
  private static readonly HrActionType[] DatedKinds =
    Enum.GetValues<HrActionType>().Where(HrActionRequest.TakesEffectOnItsDate).ToArray();

  public async Task<Result<GetHrActionsQueryResult>> Handle(GetHrActionsQuery query, CancellationToken cancellationToken)
  {
    var today = clock.Today;
    var actions = context.HrActions.AsNoTracking();
    if (query.Status is { } status)
      actions = actions.Where(a => a.Status == status);
    if (query.ActionType is { } type)
      actions = actions.Where(a => a.ActionType == type);
    if (query.EmployeeId is { } employee)
    {
      var employeeId = EmployeeId.Of(employee);
      actions = actions.Where(a => a.EmployeeId == employeeId);
    }
    if (query.From is { } from)
      actions = actions.Where(a => a.EffectiveDate >= from);
    if (query.To is { } to)
      actions = actions.Where(a => a.EffectiveDate <= to);
    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var pattern = SearchPattern.Contains(query.Search.Trim());
      actions = actions.Where(a => (a.OrderNumber != null && EF.Functions.Like(a.OrderNumber.ToLower(), pattern, SearchPattern.Escape))
        || context.Employees.Any(e => e.Id == a.EmployeeId && (EF.Functions.Like(e.EmployeeNumber.ToLower(), pattern, SearchPattern.Escape)
          || EF.Functions.Like(e.FullName!.ToLower(), pattern, SearchPattern.Escape))));
    }
    if (query.OrgUnitId is { } unit)
    {
      var units = (await lookup.SubtreeAsync(OrganizationUnitId.Of(unit), today, cancellationToken)).ToList();
      actions = actions.Where(a => (a.OldOrgUnitId != null && units.Contains(a.OldOrgUnitId)) || (a.NewOrgUnitId != null && units.Contains(a.NewOrgUnitId)));
    }

    var me = currentUser.UserId;
    switch (query.View)
    {
      case HrActionView.Mine:
        actions = actions.Where(a => a.CreatedBy == me || a.SubmittedBy == me);
        break;
      case HrActionView.ToApprove:
        actions = actions.Where(a => a.Status == HrActionStatus.Pending);
        if (await duty.IsOnAsync(DutyRules.HrAction, cancellationToken))
          actions = actions.Where(a => a.CreatedBy != me && a.SubmittedBy != me);
        break;
      case HrActionView.ToApply:
        actions = actions.Where(a => a.Status == HrActionStatus.Approved && (!DatedKinds.Contains(a.ActionType) || a.EffectiveDate <= today));
        break;
      case HrActionView.Waiting:
        actions = actions.Where(a => a.Status == HrActionStatus.Approved && DatedKinds.Contains(a.ActionType) && a.EffectiveDate > today);
        break;
    }

    var total = await actions.LongCountAsync(cancellationToken);
    var ordered = query.OldestFirst ? actions.OrderBy(a => a.CreatedAt) : actions.OrderByDescending(a => a.CreatedAt);
    var page = await ordered
      .Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize)
      .ToListAsync(cancellationToken);

    var data = await MapAsync(page, cancellationToken);
    return Result<GetHrActionsQueryResult>.Success(new(new PaginatedResult<HrActionDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }

  public async Task<Result<GetHrActionQueryResult>> Handle(GetHrActionQuery query, CancellationToken cancellationToken)
  {
    var actionId = HrActionRequestId.Of(query.Id);
    var action = await context.HrActions.AsNoTracking().FirstOrDefaultAsync(a => a.Id == actionId, cancellationToken)
      ?? throw new HrActionNotFoundException($"HR action {query.Id} was not found.");
    return Result<GetHrActionQueryResult>.Success(new((await MapAsync([action], cancellationToken))[0]));
  }

  public async Task<Result<HrActionOptionsDto>> Handle(GetHrActionOptionsQuery query, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(query.EmployeeId, cancellationToken);
    var date = query.Date ?? clock.Today;
    var probe = new HrActionDetails(HrActionType.Other, null, null, null, date, null, null, null);
    var facts = await checks.FactsAsync(employee, probe, cancellationToken);

    var options = Enum.GetValues<HrActionType>().Where(t => t != HrActionType.Other).Select(t =>
    {
      var reason = HrActionRules.Fit(t, facts);
      var needsPost = HrActionRequest.NeedsNewPost(t)
        || (t is HrActionType.Joining or HrActionType.Reinstatement && facts.Current is null && employee.EmploymentStatus != EmploymentStatus.Suspended);
      string? rule = needsPost ? t switch { HrActionType.Promotion => "higher", HrActionType.Demotion => "lower", HrActionType.Transfer => "same", _ => "any" } : null;
      return new HrActionOptionDto(t, reason is null, reason, needsPost, rule, HrActionRequest.TakesEffectOnItsDate(t));
    }).ToList();

    var events = await context.ServiceEventTypes.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.Name).ToListAsync(cancellationToken);
    var custom = events.Where(t => !ServiceEventNames.IsSystem(t.Name)).Select(t => new HrActionEventOptionDto(t.Id.Value, t.Name, t.Category)).ToList();

    HeldPostDto? current = null;
    if (facts.Current is { } held)
    {
      var on = (await lookup.PostsOnAsync([held.PostId], date, cancellationToken)).GetValueOrDefault(held.PostId.Value);
      current = new HeldPostDto(held.PostId.Value, held.PostCode, on?.Designation, on?.OrgUnit, held.Bps, held.Since);
    }

    // the lien: the post released at the last deputation out (the joining comes back to it)
    var lien = await (
        from h in context.ServiceHistory.AsNoTracking()
        where h.EmployeeId == employee.Id && h.OldPostId != null
        join t in context.ServiceEventTypes.AsNoTracking() on h.EventTypeId equals t.Id
        where t.Name == ServiceEventNames.DeputationOut
        orderby h.EffectiveDate descending, h.CreatedAt descending
        select new { h.OldPostId })
      .FirstOrDefaultAsync(cancellationToken);
    var lienCode = lien?.OldPostId is { } lienPost ? (await lookup.PostCodesAsync([lienPost], cancellationToken)).GetValueOrDefault(lienPost.Value) : null;

    return Result<HrActionOptionsDto>.Success(new(employee.Id.Value, date, employee.EmploymentStatus, employee.EmploymentBasis, current,
      await CurrentPayAsync(employee.Id, date, cancellationToken), lien?.OldPostId?.Value, lienCode, await duty.IsOnAsync(DutyRules.HrAction, cancellationToken), options, custom));
  }

  public async Task<Result<IReadOnlyList<HrActionPostDto>>> Handle(GetHrActionPostsQuery query, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(query.EmployeeId, cancellationToken);
    var date = query.Date;
    var held = await checks.HeldAsync(employee.Id, date, cancellationToken);

    var versions = await context.PostVersions.AsNoTracking()
      .Where(v => v.EffectiveFrom <= date && (v.EffectiveTo == null || v.EffectiveTo >= date) && v.LifecycleStatus != PostLifecycle.Abolished)
      .ToListAsync(cancellationToken);
    var postIds = versions.Select(v => v.PostId).ToList();
    var posts = await lookup.PostsOnAsync(postIds, date, cancellationToken);
    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var term = query.Search.Trim();
      posts = posts.Where(p => p.Value.PostCode.Contains(term, StringComparison.OrdinalIgnoreCase)
        || p.Value.Designation.Contains(term, StringComparison.OrdinalIgnoreCase)
        || p.Value.OrgUnit.Contains(term, StringComparison.OrdinalIgnoreCase)).ToDictionary(p => p.Key, p => p.Value);
    }

    var holders = await context.PositionAssignments.AsNoTracking()
      .Where(a => a.Status == RecordStatus.Active && a.AssignmentType == AssignmentType.Regular && (a.EffectiveTo == null || a.EffectiveTo >= date))
      .ToListAsync(cancellationToken);
    var byPost = holders.GroupBy(a => a.PostId.Value).ToDictionary(g => g.Key, g => g.ToList());
    var gradesWithScale = (await context.PayScaleVersions.AsNoTracking()
        .Where(v => v.Status == RecordStatus.Active && v.EffectiveFrom <= date && (v.EffectiveTo == null || v.EffectiveTo >= date))
        .Select(v => v.GradeId).ToListAsync(cancellationToken))
      .Select(g => g.Value).ToHashSet();
    var needsScale = query.ActionType != HrActionType.Transfer;

    var list = new List<HrActionPostDto>();
    foreach (var post in posts.Values.OrderBy(p => p.PostCode))
    {
      if (held is not null && post.PostId == held.PostId.Value)
        continue;

      var taken = PostCapacity.MostHeld(byPost.GetValueOrDefault(post.PostId) ?? [], new DateRange(date, null), null, employee.Id);
      string? reason = null;
      if (held is not null && query.ActionType == HrActionType.Promotion && post.Bps <= held.Bps)
        reason = $"BPS-{post.Bps}: a promotion needs a grade above BPS-{held.Bps}.";
      else if (held is not null && query.ActionType == HrActionType.Demotion && post.Bps >= held.Bps)
        reason = $"BPS-{post.Bps}: a demotion needs a grade below BPS-{held.Bps}.";
      else if (held is not null && query.ActionType == HrActionType.Transfer && post.Bps != held.Bps)
        reason = $"BPS-{post.Bps}: a transfer stays in BPS-{held.Bps}.";
      else if (post.Lifecycle != PostLifecycle.Sanctioned)
        reason = $"The post is {EnumText.Words(post.Lifecycle)}.";
      else if (taken >= post.SanctionedCount)
        reason = $"Full: {taken} of {post.SanctionedCount} seat(s) held.";
      else if (needsScale && !gradesWithScale.Contains(post.GradeId))
        reason = $"No pay scale of BPS-{post.Bps} is in force on {date:dd'/'MM'/'yyyy}.";

      // a grade the move can never take is left out; a post that only waits for a seat or a scale is shown disabled
      if (reason is not null && reason.StartsWith("BPS-", StringComparison.Ordinal))
        continue;

      list.Add(new HrActionPostDto(post.PostId, post.PostCode, post.Designation, post.OrgUnit, post.Bps, post.SanctionedCount, taken, reason is null, reason));
    }

    return Result<IReadOnlyList<HrActionPostDto>>.Success(list.OrderByDescending(p => p.Available).ThenBy(p => p.PostCode).ToList());
  }

  public async Task<Result<HrActionPreviewDto>> Handle(PreviewHrActionQuery query, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(query.EmployeeId, cancellationToken);
    HrActionDetails details;
    try
    {
      details = (await ResolveAsync(employee.Id, query.Action, cancellationToken)).Details;
    }
    catch (DomainException e)
    {
      return Result<HrActionPreviewDto>.Success(new(false, DomainMessages.Text(e), null, null, null, null, null, null, query.Action.EffectiveDate, null));
    }

    var facts = await checks.FactsAsync(employee, details, cancellationToken);
    var problem = HrActionRules.Why(details, facts);
    var date = details.EffectiveDate;
    var drawn = await CurrentPayAsync(employee.Id, date.AddDays(-1), cancellationToken) ?? await CurrentPayAsync(employee.Id, date, cancellationToken);
    int? bpsBefore = facts.Current?.Bps ?? drawn?.Bps;
    int? bpsAfter = bpsBefore;
    decimal? payBefore = drawn?.BasicPay;
    decimal? payAfter = payBefore;
    int? stageAfter = drawn?.StageNumber;
    EmploymentStatus? statusAfter = null;
    string? line = null;
    var day = date.ToString("dd'/'MM'/'yyyy");

    if (problem is null)
    {
      try
      {
        var target = facts.Target;
        switch (details.ActionType)
        {
          case HrActionType.Appointment:
          case HrActionType.DeputationIn:
          {
            var scale = await pay.ScaleInForceAsync(details.NewGradeId!, date, cancellationToken);
            var stage = scale.Stage(details.StageNumber ?? 0) ?? throw new DomainException($"The scale in force has no stage {details.StageNumber}.");
            bpsAfter = target!.Bps;
            payAfter = details.BasicPay ?? stage.BasicPay;
            stageAfter = stage.StageNumber;
            line = $"{(details.ActionType == HrActionType.Appointment ? "Appointed to" : "Joins on deputation on")} post {target.PostCode} (BPS-{target.Bps}) from {day}; pay Rs {payAfter:#,##0} (stage {stage.StageNumber}).";
            break;
          }
          case HrActionType.Promotion:
          case HrActionType.Demotion:
          {
            var scale = await pay.ScaleInForceAsync(details.NewGradeId!, date, cancellationToken);
            var stage = details.ActionType == HrActionType.Promotion ? scale.StageAtOrAbove(payBefore ?? 0) : scale.StageAtOrBelow(payBefore ?? 0);
            bpsAfter = target!.Bps;
            payAfter = stage.BasicPay;
            stageAfter = stage.StageNumber;
            line = $"BPS-{bpsBefore} → BPS-{bpsAfter}, pay Rs {payBefore:#,##0} → Rs {payAfter:#,##0} (stage {stage.StageNumber}) from {day}.";
            break;
          }
          case HrActionType.Transfer:
            line = $"Moves from post {facts.Current!.PostCode} to {target!.PostCode} (BPS-{target.Bps}) from {day}; the pay stays Rs {payBefore:#,##0}.";
            break;
          case HrActionType.Lwop:
          {
            var days = details.EndDate!.Value.DayNumber - date.DayNumber + 1;
            line = $"No pay from {day} to {details.EndDate:dd'/'MM'/'yyyy} ({days} day(s)); the next increment moves back by the same days.";
            break;
          }
          case HrActionType.Suspension:
            statusAfter = EmploymentStatus.Suspended;
            line = $"Suspended from {day}; the post is kept.";
            break;
          case HrActionType.DeputationOut:
            statusAfter = EmploymentStatus.DeputedOut;
            line = $"Deputed to {details.ExternalReferenceOrg} from {day}; post {facts.Current?.PostCode ?? "-"} is released and kept on lien. GDA stops paying from that day.";
            break;
          case HrActionType.Regularization:
            line = $"Becomes a regular employee from {day}.";
            break;
          case HrActionType.Joining:
          case HrActionType.Reinstatement:
            statusAfter = EmploymentStatus.Active;
            if (target is not null)
            {
              bpsAfter = target.Bps;
              line = $"Back on duty from {day} on post {target.PostCode} (BPS-{target.Bps}); the pay reopens at the stage last drawn.";
            }
            else
            {
              line = $"Back on duty from {day} on post {facts.Current?.PostCode ?? "-"}.";
            }
            break;
          default:
            if (HrActionRequest.SeparationFor(details.ActionType) is { } separation)
            {
              statusAfter = ServiceEventNames.StatusAfter(separation);
              payAfter = null;
              line = $"Service ends on {day} ({EnumText.Words(statusAfter.Value)}); pay stops after that day. Open HR actions, leave, tasks and requests are closed.";
            }
            else if (facts.EventType is { } eventType)
            {
              line = $"'{eventType.Name}' is recorded in the service book on {day}.";
            }
            break;
        }
      }
      catch (DomainException e)
      {
        problem = DomainMessages.Text(e);
      }
    }

    if (problem is null && HrActionRequest.TakesEffectOnItsDate(details.ActionType) && date > clock.Today)
      line += $" It can be applied on {day}, once approved.";

    return Result<HrActionPreviewDto>.Success(new(problem is null, problem, problem is null ? line : null, bpsBefore, bpsAfter, payBefore, payAfter, stageAfter, date, statusAfter));
  }

  public async Task<Result<CreatedResult>> Handle(CreateHrActionCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.EmployeeId, cancellationToken);
    var (details, current) = await ResolveAsync(employee.Id, command.Action, cancellationToken);

    var open = await context.HrActions.AnyAsync(a => a.EmployeeId == employee.Id && a.ActionType == details.ActionType
      && (a.Status == HrActionStatus.Draft || a.Status == HrActionStatus.Pending || a.Status == HrActionStatus.Approved), cancellationToken);
    if (open)
      return Result<CreatedResult>.Failure($"{employee.DisplayName} already has an open {EnumText.Words(details.ActionType)} action. Finish or cancel it first.");

    await checks.EnsureAllowedAsync(employee, details, cancellationToken);
    var action = HrActionRequest.Draft(HrActionRequestId.New(), employee, details, current);
    context.HrActions.Add(action);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(action.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(UpdateHrActionCommand command, CancellationToken cancellationToken)
  {
    var action = await context.LoadHrActionAsync(command.Id, cancellationToken);
    var employee = await context.LoadEmployeeAsync(action.EmployeeId.Value, cancellationToken);
    if (command.Action.ActionType != action.ActionType)
      return Result<UpdatedResult>.Failure("The kind of an HR action cannot change; cancel it and draft the right one.");

    var (details, current) = await ResolveAsync(action.EmployeeId, command.Action, cancellationToken);
    await checks.EnsureAllowedAsync(employee, details, cancellationToken);
    action.UpdateDraft(details, current);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(MoveHrActionCommand command, CancellationToken cancellationToken)
  {
    var action = await context.LoadHrActionAsync(command.Id, cancellationToken);
    var now = clock.UtcNow;
    string verb;
    switch (command.Step)
    {
      case HrActionStep.Submit:
        await EnsureStillAllowedAsync(action, cancellationToken);
        action.Submit(currentUser.UserId, now);
        verb = HrmVerbs.Submitted;
        break;
      case HrActionStep.Return:
        await duty.EnsureNotOwnRecordAsync(action.EmployeeId, "decide an HR action", action, cancellationToken);
        action.ReturnToDraft(currentUser.UserId, now, command.Remarks);
        verb = HrmVerbs.Returned;
        break;
      case HrActionStep.Approve:
        await duty.EnsureNotOwnRecordAsync(action.EmployeeId, "approve an HR action", action, cancellationToken);
        // maker-checker (HR_ACTION): not who drafted or submitted it
        await duty.EnsureSeparateAsync(DutyRules.HrAction, action.CreatedBy, "approve an HR action you drafted", action, cancellationToken);
        await duty.EnsureSeparateAsync(DutyRules.HrAction, action.SubmittedBy, "approve an HR action you submitted", action, cancellationToken);
        await EnsureStillAllowedAsync(action, cancellationToken);
        action.Approve(currentUser.UserId, now, command.ApprovalRequestId, requireSeparateApprover: false);
        verb = HrmVerbs.Approved;
        break;
      case HrActionStep.Reject:
        await duty.EnsureNotOwnRecordAsync(action.EmployeeId, "decide an HR action", action, cancellationToken);
        action.Reject(currentUser.UserId, now, command.Remarks);
        verb = HrmVerbs.Rejected;
        break;
      default:
        action.Cancel(currentUser.UserId, now, command.Remarks);
        verb = HrmVerbs.Cancelled;
        break;
    }

    audit.Record(verb, action, string.IsNullOrWhiteSpace(command.Remarks) ? null : $"Remarks: {command.Remarks.Trim()}");
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<ApplyHrActionResult>> Handle(ApplyHrActionCommand command, CancellationToken cancellationToken)
  {
    var action = await context.LoadHrActionAsync(command.Id, cancellationToken);
    await duty.EnsureNotOwnRecordAsync(action.EmployeeId, "apply an HR action", action, cancellationToken);
    var applied = await applier.ApplyAsync(action, command.Input, cancellationToken);
    audit.Record(HrmVerbs.Applied, action, $"{EnumText.Label(action.ActionType)} from {action.EffectiveDate:yyyy-MM-dd}");
    await context.SaveChangesAsync(cancellationToken);
    return Result<ApplyHrActionResult>.Success(new(applied.History.Id.Value, applied.Checklist, applied.PayrollImpact));
  }

  /// The rules with today's facts: a seat taken or a status changed since the draft is caught before it moves on.
  private async Task EnsureStillAllowedAsync(HrActionRequest action, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(action.EmployeeId.Value, cancellationToken);
    await checks.EnsureAllowedAsync(employee, action.Details, cancellationToken);
  }

  /// The new grade and unit from the new post (on the effective date) and the employee's current post as the old side.
  private async Task<(HrActionDetails Details, ServiceChange Current)> ResolveAsync(EmployeeId employeeId, HrActionInput input, CancellationToken cancellationToken)
  {
    PostId? newPostId = null;
    PayScaleGradeId? newGrade = null;
    OrganizationUnitId? newUnit = null;

    if (input.NewPostId is { } post)
    {
      var wanted = PostId.Of(post);
      var newPost = await context.Posts.AsNoTracking().Include(p => p.Versions).FirstOrDefaultAsync(p => p.Id == wanted, cancellationToken)
        ?? throw new PostNotFoundException($"Post {post} was not found.");
      var version = newPost.VersionOn(input.EffectiveDate)
        ?? throw new DomainException($"Post {newPost.PostCode} does not exist on {input.EffectiveDate:dd'/'MM'/'yyyy}.");
      newPostId = newPost.Id;
      newGrade = version.GradeId;
      newUnit = version.OrgUnitId;
    }

    EmployeeDocumentId? documentId = null;
    if (input.SupportingDocumentId is { } doc)
    {
      var document = await context.LoadDocumentAsync(doc, cancellationToken);
      if (document.EmployeeId != employeeId)
        throw new DomainException("The supporting document belongs to a different employee.");
      documentId = document.Id;
    }

    var current = await reader.RegularPostOnAsync(employeeId, input.EffectiveDate, cancellationToken);
    var details = new HrActionDetails(input.ActionType, newPostId, newGrade, newUnit, input.EffectiveDate, input.OrderNumber, input.Reason, documentId,
      input.EndDate, input.EventTypeId is { } eventType ? ServiceEventTypeId.Of(eventType) : null, input.ExternalReferenceOrg,
      input.EmploymentMethodId is { } employmentMethod ? global::EmploymentMethodId.Of(employmentMethod) : null,
      input.RecruitmentMethodId is { } method ? RecruitmentMethodId.Of(method) : null, input.StageNumber, input.BasicPay, input.NoticePeriodDays);
    return (details, current?.AsOld() ?? ServiceChange.None);
  }

  private async Task<CurrentPayDto?> CurrentPayAsync(EmployeeId employeeId, DateOnly date, CancellationToken cancellationToken)
  {
    var record = await context.PayRecords.AsNoTracking()
      .Where(p => p.EmployeeId == employeeId && p.EffectiveFrom <= date && (p.EffectiveTo == null || p.EffectiveTo >= date))
      .FirstOrDefaultAsync(cancellationToken);
    if (record is null)
      return null;

    var stage = await context.PayScaleStages.AsNoTracking().FirstAsync(s => s.Id == record.PayScaleStageId, cancellationToken);
    var scale = await context.PayScaleVersions.AsNoTracking().FirstAsync(v => v.Id == stage.PayScaleVersionId, cancellationToken);
    var bps = await context.PayScaleGrades.AsNoTracking().Where(g => g.Id == scale.GradeId).Select(g => g.BpsNumber).FirstAsync(cancellationToken);
    return new CurrentPayDto(record.Id.Value, bps, scale.VersionLabel, stage.StageNumber, record.BasicPay, record.EffectiveFrom, record.NextIncrementDate,
      record.IsIncrementOverdue(clock.Today), record.IncrementDeferredDays);
  }

  private async Task<List<HrActionDto>> MapAsync(List<HrActionRequest> actions, CancellationToken cancellationToken)
  {
    var today = clock.Today;
    var people = await lookup.EmployeesAsync(actions.Select(a => (EmployeeId?)a.EmployeeId), cancellationToken);
    var posts = await lookup.PostCodesAsync(actions.SelectMany(a => new[] { a.OldPostId, a.NewPostId }), cancellationToken);
    var grades = await lookup.GradesAsync(cancellationToken);
    // a unit is named as it was on the action's date
    var unitIds = actions.SelectMany(a => new[] { a.OldOrgUnitId, a.NewOrgUnitId }).Where(u => u is not null).Select(u => u!).Distinct().ToList();
    var unitVersions = await context.OrganizationUnitVersions.AsNoTracking().Where(v => unitIds.Contains(v.OrgUnitId)).ToListAsync(cancellationToken);
    string? UnitOn(OrganizationUnitId? unit, DateOnly date)
    {
      if (unit is null)
        return null;
      var versions = unitVersions.Where(v => v.OrgUnitId == unit).ToList();
      return (versions.FirstOrDefault(v => v.Range.Contains(date)) ?? versions.OrderByDescending(v => v.EffectiveFrom).FirstOrDefault())?.Name;
    }
    var eventIds = actions.Where(a => a.EventTypeId is not null).Select(a => a.EventTypeId!).Distinct().ToList();
    var events = await context.ServiceEventTypes.AsNoTracking().Where(t => eventIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

    return actions.Select(a =>
    {
      var dated = HrActionRequest.TakesEffectOnItsDate(a.ActionType);
      return new HrActionDto(
        a.Id.Value, a.ActionType, a.EmployeeId.Value, people.GetValueOrDefault(a.EmployeeId.Value)?.EmployeeNumber ?? "",
        people.GetValueOrDefault(a.EmployeeId.Value)?.FullName ?? "", a.OldPostId?.Value, a.OldPostId is null ? null : posts.GetValueOrDefault(a.OldPostId.Value),
        a.NewPostId?.Value, a.NewPostId is null ? null : posts.GetValueOrDefault(a.NewPostId.Value),
        a.OldGradeId is null ? null : grades.GetValueOrDefault(a.OldGradeId.Value)?.BpsNumber,
        a.NewGradeId is null ? null : grades.GetValueOrDefault(a.NewGradeId.Value)?.BpsNumber,
        UnitOn(a.OldOrgUnitId, a.EffectiveDate), UnitOn(a.NewOrgUnitId, a.EffectiveDate),
        a.EffectiveDate, a.OrderNumber, a.Reason, a.SupportingDocumentId?.Value, a.Status, a.ApprovalRequestId, a.ApprovedBy, a.ApprovedAt,
        a.ResultingServiceHistoryId?.Value, a.CreatedBy, a.CreatedAt, a.UpdatedAt,
        a.EndDate, a.EventTypeId?.Value, a.EventTypeId is null ? null : events.GetValueOrDefault(a.EventTypeId), a.ExternalReferenceOrg, a.EmploymentMethodId?.Value,
        a.RecruitmentMethodId?.Value, a.StageNumber, a.BasicPay, a.NoticePeriodDays, a.SubmittedBy, a.SubmittedAt, a.DecidedBy, a.DecidedAt, a.DecisionRemarks,
        a.AppliedBy, a.AppliedAt, a.LeaveApplicationId?.Value, dated, a.Status == HrActionStatus.Approved && dated && a.EffectiveDate > today);
    }).ToList();
  }
}
