using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public enum EmployeeSort
{
  Number,
  Name,
  Registered,
  Retirement
}

public sealed record GetEmployeesQueryResult(PaginatedResult<EmployeeListItemDto> Employees);

/// The employee register. Search matches part of the number or name, or the CNIC (with or without dashes). Unit,
/// designation, grade and post filter on where the employee sits today (the unit includes its sub-units unless
/// IncludeSubUnits=false). RetiringWithinMonths: superannuation falls within that many months from today.
public sealed record GetEmployeesQuery(
  PaginationRequest Pagination,
  string? Search = null,
  EmploymentType? EmploymentType = null,
  IReadOnlyList<EmploymentStatus>? EmploymentStatuses = null,
  bool IncludeInactive = false,
  Guid? OrgUnitId = null,
  bool IncludeSubUnits = true,
  Guid? DesignationId = null,
  Guid? GradeId = null,
  Guid? PostId = null,
  Gender? Gender = null,
  bool? Unplaced = null,
  int? RetiringWithinMonths = null,
  EmployeeSort SortBy = EmployeeSort.Number,
  bool SortDescending = false) : IQuery<Result<GetEmployeesQueryResult>>;

public sealed record GetEmployeeQueryResult(EmployeeDto Employee);

public sealed record GetEmployeeQuery(Guid Id) : IQuery<Result<GetEmployeeQueryResult>>;

public sealed record GetNextEmployeeNumberQueryResult(string EmployeeNumber);

public sealed record GetNextEmployeeNumberQuery : IQuery<Result<GetNextEmployeeNumberQueryResult>>;

public sealed record GetEmployeePhotoQuery(Guid Id) : IQuery<Result<StoredFile>>;

public class GetEmployeesQueryValidator : AbstractValidator<GetEmployeesQuery>
{
  public GetEmployeesQueryValidator()
  {
    RuleFor(x => x.Pagination.Pageindex).GreaterThanOrEqualTo(0);
    RuleFor(x => x.Pagination.PageSize).InclusiveBetween(1, 200);
    RuleFor(x => x.RetiringWithinMonths).InclusiveBetween(0, 600).When(x => x.RetiringWithinMonths.HasValue);
  }
}

public class EmployeeQueryHandlers(
  IApplicationDbContext context,
  HrLookup lookup,
  CodeIssuer codes,
  FileUploads files,
  IOptions<HrmOptions> options,
  IClock clock) :
  IQueryHandler<GetEmployeesQuery, Result<GetEmployeesQueryResult>>,
  IQueryHandler<GetEmployeeQuery, Result<GetEmployeeQueryResult>>,
  IQueryHandler<GetNextEmployeeNumberQuery, Result<GetNextEmployeeNumberQueryResult>>,
  IQueryHandler<GetEmployeePhotoQuery, Result<StoredFile>>
{
  public async Task<Result<GetEmployeesQueryResult>> Handle(GetEmployeesQuery query, CancellationToken cancellationToken)
  {
    var today = clock.Today;
    var retirementAge = options.Value.RetirementAge;
    var employees = context.Employees.AsNoTracking();

    if (!query.IncludeInactive)
      employees = employees.Where(e => e.ProfileStatus == RecordStatus.Active);
    if (query.EmploymentType is { } type)
      employees = employees.Where(e => e.EmploymentType == type);
    if (query.EmploymentStatuses is { Count: > 0 } statuses)
      employees = employees.Where(e => statuses.Contains(e.EmploymentStatus));
    if (query.Gender is { } gender)
      employees = employees.Where(e => e.Gender == gender);

    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var term = query.Search.Trim();
      var pattern = SearchPattern.Contains(term);
      var digits = new string(term.Where(char.IsAsciiDigit).ToArray());
      string? cnic = digits.Length == 13 ? Cnic.Normalize(digits) : null;
      employees = employees.Where(e =>
        EF.Functions.Like(e.EmployeeNumber.ToLower(), pattern, SearchPattern.Escape)
        || EF.Functions.Like(e.FullName!.ToLower(), pattern, SearchPattern.Escape)
        || (cnic != null && e.Cnic == cnic));
    }

    if (query.RetiringWithinMonths is { } months)
    {
      var latestBirth = today.AddYears(-retirementAge).AddMonths(months);
      var earliestBirth = today.AddYears(-retirementAge);
      employees = employees.Where(e => e.DateOfBirth != null && e.DateOfBirth >= earliestBirth && e.DateOfBirth <= latestBirth);
    }

    // where they sit today
    var placements =
      from a in context.PositionAssignments
      where a.AssignmentType == AssignmentType.Regular && a.Status == RecordStatus.Active && a.EffectiveFrom <= today && (a.EffectiveTo == null || a.EffectiveTo >= today)
      join v in context.PostVersions on a.PostId equals v.PostId
      where v.EffectiveFrom <= today && (v.EffectiveTo == null || v.EffectiveTo >= today)
      select new { a.EmployeeId, a.PostId, v.OrgUnitId, v.DesignationId, v.GradeId };

    if (query.OrgUnitId is { } unit)
    {
      var root = OrganizationUnitId.Of(unit);
      var units = query.IncludeSubUnits ? (await lookup.SubtreeAsync(root, today, cancellationToken)).ToList() : [root];
      employees = employees.Where(e => placements.Any(p => p.EmployeeId == e.Id && units.Contains(p.OrgUnitId)));
    }
    if (query.DesignationId is { } designation)
    {
      var designationId = DesignationId.Of(designation);
      employees = employees.Where(e => placements.Any(p => p.EmployeeId == e.Id && p.DesignationId == designationId));
    }
    if (query.GradeId is { } grade)
    {
      var gradeId = PayScaleGradeId.Of(grade);
      employees = employees.Where(e => placements.Any(p => p.EmployeeId == e.Id && p.GradeId == gradeId));
    }
    if (query.PostId is { } post)
    {
      var postId = PostId.Of(post);
      employees = employees.Where(e => placements.Any(p => p.EmployeeId == e.Id && p.PostId == postId));
    }
    if (query.Unplaced is { } unplaced)
      employees = unplaced ? employees.Where(e => !placements.Any(p => p.EmployeeId == e.Id)) : employees.Where(e => placements.Any(p => p.EmployeeId == e.Id));

    var total = await employees.LongCountAsync(cancellationToken);

    var desc = query.SortDescending;
    var ordered = query.SortBy switch
    {
      EmployeeSort.Name => desc ? employees.OrderByDescending(e => e.FullName) : employees.OrderBy(e => e.FullName),
      EmployeeSort.Registered => desc ? employees.OrderByDescending(e => e.CreatedAt) : employees.OrderBy(e => e.CreatedAt),
      // the earliest birth retires first; no date of birth last
      EmployeeSort.Retirement => desc
        ? employees.OrderBy(e => e.DateOfBirth == null).ThenByDescending(e => e.DateOfBirth)
        : employees.OrderBy(e => e.DateOfBirth == null).ThenBy(e => e.DateOfBirth),
      _ => desc ? employees.OrderByDescending(e => e.EmployeeNumber) : employees.OrderBy(e => e.EmployeeNumber)
    };

    var page = await ordered.ThenBy(e => e.EmployeeNumber)
      .Skip(query.Pagination.Pageindex * query.Pagination.PageSize)
      .Take(query.Pagination.PageSize)
      .ToListAsync(cancellationToken);

    var ids = page.Select(e => e.Id).ToList();
    var placed = await lookup.PlacementsAsync(ids, today, cancellationToken);
    var mobiles = (await context.EmployeeContacts.AsNoTracking()
        .Where(c => ids.Contains(c.EmployeeId) && c.IsPrimary && (c.ContactType == ContactType.Mobile || c.ContactType == ContactType.Phone))
        .ToListAsync(cancellationToken))
      .GroupBy(c => c.EmployeeId.Value)
      .ToDictionary(g => g.Key, g => g.OrderBy(c => c.ContactType).First().Value);

    var data = page.Select(e => new EmployeeListItemDto(
      e.Id.Value, e.EmployeeNumber, e.DisplayName, e.Cnic, e.Gender, e.DateOfBirth, e.EmploymentType, e.EmploymentMethod, e.EmploymentStatus,
      e.ProfileStatus, e.UserId, placed.GetValueOrDefault(e.Id.Value)?.ToDto(), e.SuperannuationDate(retirementAge),
      mobiles.GetValueOrDefault(e.Id.Value), e.CreatedAt)).ToList();

    return Result<GetEmployeesQueryResult>.Success(new(new PaginatedResult<EmployeeListItemDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }

  public async Task<Result<GetEmployeeQueryResult>> Handle(GetEmployeeQuery query, CancellationToken cancellationToken)
  {
    var employeeId = EmployeeId.Of(query.Id);
    var employee = await context.Employees.AsNoTracking()
        .Include(e => e.Contacts).Include(e => e.Addresses).Include(e => e.EmergencyContacts).Include(e => e.FamilyMembers)
        .AsSplitQuery()
        .FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken)
      ?? throw new EmployeeNotFoundException($"Employee {query.Id} was not found.");

    var today = clock.Today;
    var placement = (await lookup.PlacementsAsync([employee.Id], today, cancellationToken)).GetValueOrDefault(employee.Id.Value);
    var currentPay = await CurrentPayAsync(employee.Id, today, cancellationToken);

    // service starts with the first appointment / deputation-in, or the first post held
    var entryEvents = new[] { ServiceEventNames.Appointment, ServiceEventNames.DeputationIn };
    var firstEntry = await context.ServiceHistory.AsNoTracking()
      .Where(h => h.EmployeeId == employee.Id && context.ServiceEventTypes.Any(t => t.Id == h.EventTypeId && entryEvents.Contains(t.Name)))
      .OrderBy(h => h.EffectiveDate).Select(h => (DateOnly?)h.EffectiveDate).FirstOrDefaultAsync(cancellationToken);
    var firstPost = await context.PositionAssignments.AsNoTracking()
      .Where(a => a.EmployeeId == employee.Id && a.Status == RecordStatus.Active)
      .OrderBy(a => a.EffectiveFrom).Select(a => (DateOnly?)a.EffectiveFrom).FirstOrDefaultAsync(cancellationToken);

    var dto = new EmployeeDto(
      employee.Id.Value, employee.EmployeeNumber, employee.FirstName, employee.MiddleName, employee.LastName, employee.DisplayName,
      employee.Cnic, employee.DateOfBirth, employee.Gender, employee.Nationality, employee.MaritalStatus, employee.BloodGroup,
      employee.EmploymentType, employee.EmploymentMethod, employee.ProjectId, employee.EmploymentStatus, employee.ProfileStatus,
      employee.UserId, employee.PhotoReference is not null, employee.SuperannuationDate(options.Value.RetirementAge), firstEntry ?? firstPost,
      placement?.ToDto(), currentPay,
      employee.Contacts.OrderBy(c => c.ContactType).ThenByDescending(c => c.IsPrimary).Select(c => c.ToDto()).ToList(),
      employee.Addresses.OrderBy(a => a.AddressType).Select(a => a.ToDto()).ToList(),
      employee.EmergencyContacts.Select(c => c.ToDto()).ToList(),
      employee.FamilyMembers.Select(m => m.ToDto()).ToList(),
      employee.CreatedAt, employee.UpdatedAt);

    return Result<GetEmployeeQueryResult>.Success(new(dto));
  }

  public async Task<Result<GetNextEmployeeNumberQueryResult>> Handle(GetNextEmployeeNumberQuery query, CancellationToken cancellationToken) =>
      Result<GetNextEmployeeNumberQueryResult>.Success(new(await codes.NextEmployeeNumberAsync(cancellationToken)));

  public async Task<Result<StoredFile>> Handle(GetEmployeePhotoQuery query, CancellationToken cancellationToken)
  {
    var employeeId = EmployeeId.Of(query.Id);
    var employee = await context.Employees.AsNoTracking().Where(e => e.Id == employeeId)
        .Select(e => new { e.EmployeeNumber, e.PhotoReference }).FirstOrDefaultAsync(cancellationToken)
      ?? throw new EmployeeNotFoundException($"Employee {query.Id} was not found.");

    if (employee.PhotoReference is null)
      throw new StoredFileNotFoundException("The employee has no photo.");

    return Result<StoredFile>.Success(await files.OpenAsync(employee.PhotoReference, $"{employee.EmployeeNumber}-photo", cancellationToken));
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
    return new CurrentPayDto(record.Id.Value, bps, scale.VersionLabel, stage.StageNumber, record.BasicPay, record.EffectiveFrom, record.NextIncrementDate);
  }
}
