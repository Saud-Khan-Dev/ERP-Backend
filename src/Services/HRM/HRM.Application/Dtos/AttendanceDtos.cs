public sealed record WorkShiftDto(Guid Id, string Name, TimeOnly StartTime, TimeOnly EndTime, int GraceMinutes, IReadOnlyList<int> WorkingWeekdays, decimal ScheduledHours, bool IsActive);

public sealed record EmployeeShiftDto(Guid Id, Guid EmployeeId, Guid WorkShiftId, string? WorkShift, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

public sealed record HolidayDto(Guid Id, DateOnly HolidayDate, string Name, HolidayType HolidayType, Guid? LocationId, string? Location, string? NotificationRef);

public sealed record AttendanceRecordDto(
  Guid Id,
  Guid EmployeeId,
  string EmployeeNumber,
  string EmployeeName,
  DateOnly AttendanceDate,
  Guid? WorkShiftId,
  string? WorkShift,
  TimeOnly? CheckIn,
  TimeOnly? CheckOut,
  decimal? WorkingHours,
  decimal? OvertimeHours,
  int LateMinutes,
  int EarlyDepartureMinutes,
  AttendanceStatus? Status,
  string? Remarks);

public sealed record AttendanceSummaryDto(
  Guid EmployeeId,
  string EmployeeNumber,
  string EmployeeName,
  int Present,
  int Late,
  int HalfDay,
  int Absent,
  int OnLeave,
  int OfficialDuty,
  int Holiday,
  int Weekend,
  int LateMinutes,
  decimal WorkingHours,
  decimal OvertimeHours);

public sealed record LeaveTypeDto(
  Guid Id,
  string Name,
  bool IsPaid,
  decimal? MaxDaysPerYear,
  string? AccrualRule,
  bool CarryForwardAllowed,
  bool AffectsPayroll,
  bool IsBalanceTracked);

public sealed record LeaveEntitlementDto(Guid Id, Guid EmployeeId, Guid LeaveTypeId, string? LeaveType, int Year, decimal EntitledDays);

public sealed record LeaveBalanceDto(
  Guid LeaveTypeId,
  string? LeaveType,
  int Year,
  decimal EntitledDays,
  decimal AccruedDays,
  decimal UsedDays,
  decimal BalanceDays,
  decimal PendingDays,
  decimal AvailableDays);

public sealed record LeaveApplicationDto(
  Guid Id,
  Guid EmployeeId,
  string EmployeeNumber,
  string EmployeeName,
  Guid LeaveTypeId,
  string? LeaveType,
  DateOnly StartDate,
  DateOnly EndDate,
  decimal Days,
  LeaveStatus Status,
  DateOnly AppliedDate,
  Guid? ApprovedBy,
  DateOnly? ApprovalDate,
  string? Reason,
  DateTime? CreatedAt,
  Guid? UpdatedBy);

public sealed record LeaveLedgerEntryDto(
  Guid Id,
  Guid LeaveTypeId,
  string? LeaveType,
  int Year,
  LeaveTransactionType TransactionType,
  decimal Days,
  DateOnly TransactionDate,
  Guid? LeaveApplicationId,
  string? Remarks,
  Guid? CreatedBy,
  DateTime? CreatedAt);

public static class AttendanceMappings
{
  public static WorkShiftDto ToDto(this WorkShift x) => new(
    x.Id.Value, x.Name, x.StartTime, x.EndTime, x.GraceMinutes, x.WorkingWeekdays.Select(d => (int)d).ToList(), decimal.Round(x.ScheduledHours, 2), x.IsActive);

  public static LeaveTypeDto ToDto(this LeaveType x) => new(
    x.Id.Value, x.Name, x.IsPaid, x.MaxDaysPerYear, x.AccrualRule, x.CarryForwardAllowed, x.AffectsPayroll, x.IsBalanceTracked);

  public static LeaveLedgerEntryDto ToDto(this LeaveLedgerEntry x, string? typeName) => new(
    x.Id.Value, x.LeaveTypeId.Value, typeName, x.Year, x.TransactionType, x.Days, x.TransactionDate, x.LeaveApplicationId?.Value, x.Remarks, x.CreatedBy, x.CreatedAt);
}
