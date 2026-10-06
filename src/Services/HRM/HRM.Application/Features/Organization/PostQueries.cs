using FluentValidation;
using Microsoft.EntityFrameworkCore;

public enum PostSort
{
  Code,
  Bps,
  Vacancy
}

public sealed record GetPostsQueryResult(DateOnly AsOf, PaginatedResult<PostListItemDto> Posts);

/// Posts as they are on a date (default today). OrgUnitId includes the unit's sub-units unless IncludeSubUnits=false.
/// Status: vacant / partially_filled / filled / frozen / abolished (derived from who holds the seats that day).
public sealed record GetPostsQuery(
  PaginationRequest Pagination,
  DateOnly? AsOf = null,
  Guid? OrgUnitId = null,
  bool IncludeSubUnits = true,
  Guid? DesignationId = null,
  Guid? GradeId = null,
  PositionStatus? Status = null,
  EmploymentType? EmploymentType = null,
  string? Search = null,
  PostSort SortBy = PostSort.Code,
  bool SortDescending = false) : IQuery<Result<GetPostsQueryResult>>;

public sealed record GetPostQueryResult(PostDto Post);

public sealed record GetPostQuery(Guid Id) : IQuery<Result<GetPostQueryResult>>;

public sealed record GetNextPostCodeQueryResult(string PostCode);

/// A preview only: nothing is reserved.
public sealed record GetNextPostCodeQuery : IQuery<Result<GetNextPostCodeQueryResult>>;

public class GetPostsQueryValidator : AbstractValidator<GetPostsQuery>
{
  public GetPostsQueryValidator()
  {
    RuleFor(x => x.Pagination.Pageindex).GreaterThanOrEqualTo(0);
    RuleFor(x => x.Pagination.PageSize).InclusiveBetween(1, 200);
    RuleFor(x => x.Status).Must(s => s != PositionStatus.Sanctioned).When(x => x.Status.HasValue)
      .WithMessage("Filter on vacant, partially_filled, filled, frozen or abolished.");
  }
}

public class PostQueryHandlers(IApplicationDbContext context, HrLookup lookup, CodeIssuer codes, IClock clock) :
  IQueryHandler<GetPostsQuery, Result<GetPostsQueryResult>>,
  IQueryHandler<GetPostQuery, Result<GetPostQueryResult>>,
  IQueryHandler<GetNextPostCodeQuery, Result<GetNextPostCodeQueryResult>>
{
  public async Task<Result<GetPostsQueryResult>> Handle(GetPostsQuery query, CancellationToken cancellationToken)
  {
    var date = query.AsOf ?? clock.Today;

    var versions = context.PostVersions.AsNoTracking().Where(v => v.EffectiveFrom <= date && (v.EffectiveTo == null || v.EffectiveTo >= date));

    if (query.OrgUnitId is { } unit)
    {
      var root = OrganizationUnitId.Of(unit);
      if (query.IncludeSubUnits)
      {
        var subtree = (await lookup.SubtreeAsync(root, date, cancellationToken)).ToList();
        versions = versions.Where(v => subtree.Contains(v.OrgUnitId));
      }
      else
      {
        versions = versions.Where(v => v.OrgUnitId == root);
      }
    }
    if (query.DesignationId is { } designation)
    {
      var designationId = DesignationId.Of(designation);
      versions = versions.Where(v => v.DesignationId == designationId);
    }
    if (query.GradeId is { } grade)
    {
      var gradeId = PayScaleGradeId.Of(grade);
      versions = versions.Where(v => v.GradeId == gradeId);
    }
    if (query.EmploymentType is { } employmentType)
      versions = versions.Where(v => v.EmploymentType == employmentType);

    var rows = versions.Select(v => new
    {
      Version = v,
      Code = context.Posts.Where(p => p.Id == v.PostId).Select(p => p.PostCode).First(),
      Filled = context.PositionAssignments.Count(a => a.PostId == v.PostId && a.AssignmentType == AssignmentType.Regular
        && a.Status == RecordStatus.Active && a.EffectiveFrom <= date && (a.EffectiveTo == null || a.EffectiveTo >= date))
    });

    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var pattern = SearchPattern.Contains(query.Search);
      rows = rows.Where(r => EF.Functions.Like(r.Code.ToLower(), pattern, SearchPattern.Escape));
    }

    rows = query.Status switch
    {
      PositionStatus.Abolished => rows.Where(r => r.Version.LifecycleStatus == PostLifecycle.Abolished),
      PositionStatus.Frozen => rows.Where(r => r.Version.LifecycleStatus == PostLifecycle.Frozen),
      PositionStatus.Vacant => rows.Where(r => r.Version.LifecycleStatus == PostLifecycle.Sanctioned && r.Filled == 0),
      PositionStatus.PartiallyFilled => rows.Where(r => r.Version.LifecycleStatus == PostLifecycle.Sanctioned && r.Filled > 0 && r.Filled < r.Version.SanctionedCount),
      PositionStatus.Filled => rows.Where(r => r.Version.LifecycleStatus == PostLifecycle.Sanctioned && r.Filled >= r.Version.SanctionedCount),
      _ => rows
    };

    var total = await rows.LongCountAsync(cancellationToken);

    var grades = await lookup.GradesAsync(cancellationToken);
    var ordered = query.SortBy switch
    {
      PostSort.Vacancy => query.SortDescending
        ? rows.OrderByDescending(r => r.Version.SanctionedCount - r.Filled).ThenBy(r => r.Code)
        : rows.OrderBy(r => r.Version.SanctionedCount - r.Filled).ThenBy(r => r.Code),
      PostSort.Bps => query.SortDescending
        ? rows.OrderByDescending(r => context.PayScaleGrades.Where(g => g.Id == r.Version.GradeId).Select(g => g.BpsNumber).First()).ThenBy(r => r.Code)
        : rows.OrderBy(r => context.PayScaleGrades.Where(g => g.Id == r.Version.GradeId).Select(g => g.BpsNumber).First()).ThenBy(r => r.Code),
      _ => query.SortDescending ? rows.OrderByDescending(r => r.Code) : rows.OrderBy(r => r.Code)
    };

    var page = await ordered
      .Skip(query.Pagination.Pageindex * query.Pagination.PageSize)
      .Take(query.Pagination.PageSize)
      .ToListAsync(cancellationToken);

    var designations = await lookup.DesignationsAsync(page.Select(r => r.Version.DesignationId), cancellationToken);
    var units = await lookup.UnitNamesAsync(page.Select(r => r.Version.OrgUnitId), date, cancellationToken);
    var holders = await HoldersAsync(page.Select(r => r.Version.PostId).ToList(), date, cancellationToken);

    var data = page.Select(r => new PostListItemDto(
      r.Version.PostId.Value, r.Code, r.Version.DesignationId.Value, designations.GetValueOrDefault(r.Version.DesignationId.Value),
      r.Version.GradeId.Value, grades.TryGetValue(r.Version.GradeId.Value, out var g) ? g.BpsNumber : 0,
      r.Version.OrgUnitId.Value, units.GetValueOrDefault(r.Version.OrgUnitId.Value), r.Version.EmploymentType,
      r.Version.SanctionedCount, r.Filled, OrganizationMappings.OccupancyStatus(r.Version.LifecycleStatus, r.Version.SanctionedCount, r.Filled),
      r.Version.LifecycleStatus, r.Version.EffectiveFrom, holders.GetValueOrDefault(r.Version.PostId.Value) ?? [])).ToList();

    return Result<GetPostsQueryResult>.Success(new(date, new PaginatedResult<PostListItemDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }

  public async Task<Result<GetPostQueryResult>> Handle(GetPostQuery query, CancellationToken cancellationToken)
  {
    var postId = PostId.Of(query.Id);
    var post = await context.Posts.AsNoTracking().Include(p => p.Versions).FirstOrDefaultAsync(p => p.Id == postId, cancellationToken)
      ?? throw new PostNotFoundException($"Post {query.Id} was not found.");

    var today = clock.Today;
    var versions = post.Versions.OrderByDescending(v => v.EffectiveFrom).ToList();
    var designations = await lookup.DesignationsAsync(versions.Select(v => v.DesignationId), cancellationToken);
    var grades = await lookup.GradesAsync(cancellationToken);
    var units = await lookup.UnitNamesAsync(versions.Select(v => v.OrgUnitId), today, cancellationToken);
    var reporting = await lookup.PostCodesAsync(versions.Select(v => v.ReportingPostId), cancellationToken);
    var locations = await lookup.LocationsAsync(versions.Select(v => v.LocationId), cancellationToken);

    PostVersionDto Map(PostVersion v) => new(
      v.Id.Value, v.DesignationId.Value, designations.GetValueOrDefault(v.DesignationId.Value), v.GradeId.Value,
      grades.TryGetValue(v.GradeId.Value, out var g) ? g.BpsNumber : 0, v.OrgUnitId.Value, units.GetValueOrDefault(v.OrgUnitId.Value),
      v.ReportingPostId?.Value, v.ReportingPostId is null ? null : reporting.GetValueOrDefault(v.ReportingPostId.Value), v.EmploymentType,
      v.LocationId?.Value, v.LocationId is null ? null : locations.GetValueOrDefault(v.LocationId.Value), v.SanctionedCount,
      v.LifecycleStatus, v.NotificationRef, v.EffectiveFrom, v.EffectiveTo, v.ApprovedBy);

    var assignments = await context.PositionAssignments.AsNoTracking().Where(a => a.PostId == postId)
      .OrderByDescending(a => a.EffectiveFrom).ToListAsync(cancellationToken);
    var people = await lookup.EmployeesAsync(assignments.Select(a => (EmployeeId?)a.EmployeeId), cancellationToken);
    var holders = assignments
      .Where(a => a.IsLive)
      .Select(a => new PostHolderDto(a.Id.Value, a.EmployeeId.Value, people.GetValueOrDefault(a.EmployeeId.Value)?.EmployeeNumber ?? "",
        people.GetValueOrDefault(a.EmployeeId.Value)?.FullName ?? "", a.AssignmentType, a.EffectiveFrom, a.EffectiveTo))
      .ToList();

    var current = post.VersionOn(today);
    return Result<GetPostQueryResult>.Success(new(new PostDto(post.Id.Value, post.PostCode, current is null ? null : Map(current),
      versions.Select(Map).ToList(), holders, post.CreatedAt)));
  }

  public async Task<Result<GetNextPostCodeQueryResult>> Handle(GetNextPostCodeQuery query, CancellationToken cancellationToken) =>
      Result<GetNextPostCodeQueryResult>.Success(new(await codes.NextPostCodeAsync(cancellationToken)));

  /// Everyone holding each post on the date (regular first).
  private async Task<Dictionary<Guid, List<PostHolderDto>>> HoldersAsync(List<PostId> postIds, DateOnly date, CancellationToken cancellationToken)
  {
    var assignments = await context.PositionAssignments.AsNoTracking()
      .Where(a => postIds.Contains(a.PostId) && a.Status == RecordStatus.Active && a.EffectiveFrom <= date && (a.EffectiveTo == null || a.EffectiveTo >= date))
      .ToListAsync(cancellationToken);
    var people = await lookup.EmployeesAsync(assignments.Select(a => (EmployeeId?)a.EmployeeId), cancellationToken);

    return assignments
      .OrderBy(a => a.AssignmentType).ThenBy(a => a.EffectiveFrom)
      .GroupBy(a => a.PostId.Value)
      .ToDictionary(g => g.Key, g => g.Select(a => new PostHolderDto(a.Id.Value, a.EmployeeId.Value,
        people.GetValueOrDefault(a.EmployeeId.Value)?.EmployeeNumber ?? "", people.GetValueOrDefault(a.EmployeeId.Value)?.FullName ?? "",
        a.AssignmentType, a.EffectiveFrom, a.EffectiveTo)).ToList());
  }
}
