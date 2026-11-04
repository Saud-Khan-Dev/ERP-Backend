using FluentValidation;
using Microsoft.EntityFrameworkCore;

/// One person as the rest of the ERP sees them: who they are and where they sit today - never pay, CNIC or contacts.
public sealed record DirectoryEmployeeDto(
  Guid Id,
  string EmployeeNumber,
  string Name,
  string? Designation,
  string? OrgUnit,
  Guid? OrgUnitId,
  EmploymentStatus Status,
  /// Active, on leave, suspended or deputed out - not retired, resigned, terminated or deceased.
  bool InService,
  /// The sign-in account linked to this person, if any.
  Guid? UserId);

public sealed record DirectoryOrgUnitDto(Guid Id, string Code, string Name, Guid? ParentId);

/// Who holds a post on a date (its regular holder; for a post of several seats, the longest-serving one).
public sealed record DirectoryPostHolderDto(Guid PostId, string PostCode, string Designation, DirectoryEmployeeDto? Holder);

public sealed record GetDirectoryEmployeesQueryResult(PaginatedResult<DirectoryEmployeeDto> Employees);

/// The staff directory: search by number or name (part of either), or ask for given ids. People who left service are
/// left out unless IncludeLeft - a picker for a new act offers only people in service.
public sealed record GetDirectoryEmployeesQuery(PaginationRequest Pagination, string? Search, IReadOnlyList<Guid>? Ids, bool IncludeLeft)
  : IQuery<Result<GetDirectoryEmployeesQueryResult>>;

public sealed record GetDirectoryOrgUnitsQueryResult(IReadOnlyList<DirectoryOrgUnitDto> Units);

/// The organization's units as they stand today, for pickers in other modules (an asset's department).
public sealed record GetDirectoryOrgUnitsQuery : IQuery<Result<GetDirectoryOrgUnitsQueryResult>>;

public sealed record GetPostHolderQuery(Guid PostId, DateOnly? On) : IQuery<Result<DirectoryPostHolderDto>>;

/// One office (organization unit) as Administration's "works on" picker shows it.
public sealed record DirectoryOfficeDto(Guid Id, string Code, string Name, Guid? ParentId, string? TypeName);

public sealed record GetDirectoryOfficesQueryResult(IReadOnlyList<DirectoryOfficeDto> Offices);

/// The active units of the structure today, for the data-scope picker in Administration (a person limited to an office
/// works on it and every unit below it).
public sealed record GetDirectoryOfficesQuery : IQuery<Result<GetDirectoryOfficesQueryResult>>;

/// A gazetted holiday as the rest of the ERP needs it (counting working days for notice periods).
public sealed record PublicHolidayDto(DateOnly Date, string Name, HolidayType HolidayType);

public sealed record GetPublicHolidaysQueryResult(IReadOnlyList<PublicHolidayDto> Holidays);

/// The gazetted holidays (organization-wide, not optional) between two dates - public facts, read without a token by
/// Taxation's daily job.
public sealed record GetPublicHolidaysQuery(DateOnly From, DateOnly To) : IQuery<Result<GetPublicHolidaysQueryResult>>;

public class GetPublicHolidaysQueryValidator : AbstractValidator<GetPublicHolidaysQuery>
{
  public GetPublicHolidaysQueryValidator()
  {
    RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("'to' must not be before 'from'.");
    RuleFor(x => x).Must(x => x.To.DayNumber - x.From.DayNumber <= 3 * 366).WithMessage("Ask for at most three years at a time.");
  }
}

/// A person's own email on their HR record (the primary one), for an administrator opening a sign-in account for them.
public sealed record DirectoryEmailDto(Guid EmployeeId, string? Email);
public sealed record GetDirectoryEmailQuery(Guid EmployeeId) : IQuery<Result<DirectoryEmailDto>>;

public class GetDirectoryEmployeesQueryValidator : AbstractValidator<GetDirectoryEmployeesQuery>
{
  public GetDirectoryEmployeesQueryValidator()
  {
    RuleFor(x => x.Pagination.Pageindex).GreaterThanOrEqualTo(0);
    RuleFor(x => x.Pagination.PageSize).InclusiveBetween(1, 200);
    RuleFor(x => x.Ids).Must(ids => ids is null || ids.Count <= 200).WithMessage("Ask for at most 200 people at a time.");
  }
}

/// Read by any signed-in person (Assets custodians, Taxation's signing officer, linking an account to its HR record),
/// so it carries only what a staff directory on a notice board would.
public class DirectoryHandlers(IApplicationDbContext context, HrLookup lookup, IClock clock) :
  IQueryHandler<GetDirectoryEmployeesQuery, Result<GetDirectoryEmployeesQueryResult>>,
  IQueryHandler<GetDirectoryOrgUnitsQuery, Result<GetDirectoryOrgUnitsQueryResult>>,
  IQueryHandler<GetPostHolderQuery, Result<DirectoryPostHolderDto>>,
  IQueryHandler<GetDirectoryOfficesQuery, Result<GetDirectoryOfficesQueryResult>>,
  IQueryHandler<GetPublicHolidaysQuery, Result<GetPublicHolidaysQueryResult>>,
  IQueryHandler<GetDirectoryEmailQuery, Result<DirectoryEmailDto>>
{
  public async Task<Result<DirectoryEmailDto>> Handle(GetDirectoryEmailQuery query, CancellationToken cancellationToken)
  {
    var id = EmployeeId.Of(query.EmployeeId);
    var email = await context.EmployeeContacts.AsNoTracking()
      .Where(c => c.EmployeeId == id && c.ContactType == ContactType.Email)
      .OrderByDescending(c => c.IsPrimary).Select(c => c.Value).FirstOrDefaultAsync(cancellationToken);
    return Result<DirectoryEmailDto>.Success(new(query.EmployeeId, email));
  }

  private static readonly EmploymentStatus[] InServiceStatuses =
    [EmploymentStatus.Active, EmploymentStatus.OnLeave, EmploymentStatus.Suspended, EmploymentStatus.DeputedOut];

  public async Task<Result<GetDirectoryEmployeesQueryResult>> Handle(GetDirectoryEmployeesQuery query, CancellationToken cancellationToken)
  {
    var employees = context.Employees.AsNoTracking().Where(e => e.ProfileStatus == RecordStatus.Active);

    if (query.Ids is { Count: > 0 } wanted)
    {
      // asked for by id (labelling stored references): people who left are still named
      var ids = wanted.Distinct().Select(EmployeeId.Of).ToList();
      employees = context.Employees.AsNoTracking().Where(e => ids.Contains(e.Id));
    }
    else if (!query.IncludeLeft)
    {
      employees = employees.Where(e => InServiceStatuses.Contains(e.EmploymentStatus));
    }

    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var pattern = SearchPattern.Contains(query.Search.Trim());
      employees = employees.Where(e =>
        EF.Functions.Like(e.EmployeeNumber.ToLower(), pattern, SearchPattern.Escape)
        || EF.Functions.Like(e.FullName!.ToLower(), pattern, SearchPattern.Escape));
    }

    var total = await employees.LongCountAsync(cancellationToken);
    var page = await employees.OrderBy(e => e.FullName).ThenBy(e => e.EmployeeNumber)
      .Skip(query.Pagination.Pageindex * query.Pagination.PageSize)
      .Take(query.Pagination.PageSize)
      .ToListAsync(cancellationToken);

    var placed = await lookup.PlacementsAsync(page.Select(e => e.Id), clock.Today, cancellationToken);
    var data = page.Select(e => ToDto(e, placed.GetValueOrDefault(e.Id.Value))).ToList();
    return Result<GetDirectoryEmployeesQueryResult>.Success(
      new(new PaginatedResult<DirectoryEmployeeDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }

  public async Task<Result<GetDirectoryOrgUnitsQueryResult>> Handle(GetDirectoryOrgUnitsQuery query, CancellationToken cancellationToken)
  {
    var units = await context.CurrentOrganizationUnits.AsNoTracking()
      .OrderBy(u => u.Level).ThenBy(u => u.Name)
      .Select(u => new DirectoryOrgUnitDto(u.OrgUnitId, u.Code, u.Name, u.ParentUnitId))
      .ToListAsync(cancellationToken);
    return Result<GetDirectoryOrgUnitsQueryResult>.Success(new(units));
  }

  public async Task<Result<DirectoryPostHolderDto>> Handle(GetPostHolderQuery query, CancellationToken cancellationToken)
  {
    var on = query.On ?? clock.Today;
    var postId = PostId.Of(query.PostId);
    var post = (await lookup.PostsOnAsync([postId], on, cancellationToken)).GetValueOrDefault(query.PostId)
      ?? throw new NotFoundException($"Post {query.PostId} is not on the establishment on {on:yyyy-MM-dd}.");

    var holderId = await context.PositionAssignments.AsNoTracking()
      .Where(a => a.PostId == postId && a.AssignmentType == AssignmentType.Regular && a.Status == RecordStatus.Active
        && a.EffectiveFrom <= on && (a.EffectiveTo == null || a.EffectiveTo >= on))
      .OrderBy(a => a.EffectiveFrom)
      .Select(a => a.EmployeeId)
      .FirstOrDefaultAsync(cancellationToken);

    DirectoryEmployeeDto? holder = null;
    if (holderId is not null)
    {
      var employee = await context.Employees.AsNoTracking().FirstAsync(e => e.Id == holderId, cancellationToken);
      var placement = (await lookup.PlacementsAsync([employee.Id], on, cancellationToken)).GetValueOrDefault(employee.Id.Value);
      holder = ToDto(employee, placement);
    }
    return Result<DirectoryPostHolderDto>.Success(new DirectoryPostHolderDto(post.PostId, post.PostCode, post.Designation, holder));
  }

  public async Task<Result<GetDirectoryOfficesQueryResult>> Handle(GetDirectoryOfficesQuery query, CancellationToken cancellationToken)
  {
    var today = clock.Today;
    var versions = await context.OrganizationUnitVersions.AsNoTracking()
      .Where(v => v.Status == RecordStatus.Active && v.EffectiveFrom <= today && (v.EffectiveTo == null || v.EffectiveTo >= today))
      .ToListAsync(cancellationToken);
    var unitIds = versions.Select(v => v.OrgUnitId).Distinct().ToList();
    var codes = await context.OrganizationUnits.AsNoTracking().Where(u => unitIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Code, cancellationToken);
    var types = await context.OrganizationUnitTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

    var offices = versions
      .Select(v => new DirectoryOfficeDto(v.OrgUnitId.Value, codes.GetValueOrDefault(v.OrgUnitId) ?? "", v.Name, v.ParentUnitId?.Value,
        types.GetValueOrDefault(v.UnitTypeId)))
      .OrderBy(o => o.Name).ToList();
    return Result<GetDirectoryOfficesQueryResult>.Success(new(offices));
  }

  public async Task<Result<GetPublicHolidaysQueryResult>> Handle(GetPublicHolidaysQuery query, CancellationToken cancellationToken)
  {
    var rows = await context.Holidays.AsNoTracking()
      .Where(h => h.HolidayDate >= query.From && h.HolidayDate <= query.To && h.LocationId == null && h.HolidayType != HolidayType.Optional)
      .OrderBy(h => h.HolidayDate)
      .Select(h => new PublicHolidayDto(h.HolidayDate, h.Name, h.HolidayType))
      .ToListAsync(cancellationToken);
    return Result<GetPublicHolidaysQueryResult>.Success(new(rows));
  }

  private static DirectoryEmployeeDto ToDto(Employee e, Placement? placement) => new(
    e.Id.Value, e.EmployeeNumber, e.DisplayName, placement?.Designation, placement?.OrgUnit, placement?.OrgUnitId,
    e.EmploymentStatus, InServiceStatuses.Contains(e.EmploymentStatus), e.UserId);
}
