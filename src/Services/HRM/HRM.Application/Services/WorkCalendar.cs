using Microsoft.EntityFrameworkCore;

/// One employee's working days over a range: their shift's weekdays (Monday-Friday without a shift) and the holidays
/// that apply where they work (gazetted ones everywhere, local ones at their post's or unit's location).
public sealed class EmployeeCalendar(IReadOnlyList<(DateRange Range, WorkShift Shift)> shifts, IReadOnlySet<DateOnly> holidays)
{
  public WorkShift? ShiftOn(DateOnly date) => shifts.FirstOrDefault(s => s.Range.Contains(date)).Shift;

  public bool IsWorkingDay(DateOnly date) =>
      ShiftOn(date)?.IsWorkingDay(date) ?? WorkShift.DefaultWeekdays.Contains(WorkShift.IsoWeekday(date));

  public bool IsHoliday(DateOnly date) => holidays.Contains(date);
}

public class WorkCalendar(IApplicationDbContext context)
{
  public async Task<EmployeeCalendar> ForAsync(EmployeeId employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
      (await ForManyAsync([employeeId], from, to, cancellationToken))[employeeId];

  /// Calendars for many employees in a few queries.
  public async Task<Dictionary<EmployeeId, EmployeeCalendar>> ForManyAsync(IReadOnlyCollection<EmployeeId> employeeIds, DateOnly from, DateOnly to, CancellationToken cancellationToken)
  {
    var ids = employeeIds.Distinct().ToList();

    var assignedShifts = await context.EmployeeShifts.AsNoTracking()
      .Where(s => ids.Contains(s.EmployeeId) && s.EffectiveFrom <= to && (s.EffectiveTo == null || s.EffectiveTo >= from))
      .ToListAsync(cancellationToken);
    var shiftIds = assignedShifts.Select(s => s.WorkShiftId).Distinct().ToList();
    var shifts = await context.WorkShifts.AsNoTracking().Where(s => shiftIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, cancellationToken);

    var holidays = await context.Holidays.AsNoTracking().Where(h => h.HolidayDate >= from && h.HolidayDate <= to).ToListAsync(cancellationToken);
    var locations = await LocationsAsync(ids, from, cancellationToken);

    return ids.ToDictionary(id => id, id =>
    {
      var location = locations.GetValueOrDefault(id);
      return new EmployeeCalendar(
        assignedShifts.Where(s => s.EmployeeId == id).Select(s => (s.Range, shifts[s.WorkShiftId])).ToList(),
        holidays.Where(h => h.AppliesTo(location)).Select(h => h.HolidayDate).ToHashSet());
    });
  }

  /// Where each employee works on a date: their post's location, else their unit's.
  private async Task<Dictionary<EmployeeId, LocationId?>> LocationsAsync(List<EmployeeId> ids, DateOnly date, CancellationToken cancellationToken)
  {
    var placements = await (
        from a in context.PositionAssignments
        where ids.Contains(a.EmployeeId) && a.AssignmentType == AssignmentType.Regular && a.Status == RecordStatus.Active
          && a.EffectiveFrom <= date && (a.EffectiveTo == null || a.EffectiveTo >= date)
        join v in context.PostVersions on a.PostId equals v.PostId
        where v.EffectiveFrom <= date && (v.EffectiveTo == null || v.EffectiveTo >= date)
        select new { a.EmployeeId, v.LocationId, v.OrgUnitId })
      .ToListAsync(cancellationToken);

    var unitIds = placements.Where(p => p.LocationId is null).Select(p => p.OrgUnitId).Distinct().ToList();
    var unitLocations = await context.OrganizationUnitVersions.AsNoTracking()
      .Where(v => unitIds.Contains(v.OrgUnitId) && v.EffectiveFrom <= date && (v.EffectiveTo == null || v.EffectiveTo >= date))
      .ToDictionaryAsync(v => v.OrgUnitId, v => v.LocationId, cancellationToken);

    return placements.GroupBy(p => p.EmployeeId).ToDictionary(g => g.Key, g =>
    {
      var p = g.First();
      return p.LocationId ?? unitLocations.GetValueOrDefault(p.OrgUnitId);
    });
  }
}
