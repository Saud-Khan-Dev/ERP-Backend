/// The names of HRM's runs over many records, as their single "Ran" line in the audit trail names them (and as the
/// system page lists them). Inside a run the automatic lines are off (`IAuditTrail.Quietly`); the run writes one line
/// saying who ran it, with what, and what it did.
public static class HrmRuns
{
  public const string PayrollCalculation = "Payroll calculation";
  public const string PayrollFinalization = "Payroll finalization";
  public const string SalaryPayments = "Salary payments";
  public const string Arrears = "Arrears calculation";
  public const string AnnualIncrement = "Annual increment";
  public const string PayRevision = "Pay revision";
  public const string CloseAttendanceDay = "Close attendance day";
  public const string AttendanceImport = "Attendance import";
  public const string ShiftAssignment = "Shift assignment";
  public const string LeaveEntitlements = "Leave entitlements";
  public const string LeaveYearEnd = "Leave year end";
  public const string StartReviews = "Performance reviews";
  public const string GpFundInterest = "GP Fund interest";
}

/// HRM's verbs for decisions, each its own line in the trail next to the field changes.
public static class HrmVerbs
{
  public const string Submitted = "Submitted";
  public const string Approved = "Approved";
  public const string Rejected = "Rejected";
  public const string Returned = "Returned";
  public const string Cancelled = "Cancelled";
  public const string Applied = "Applied";
  public const string Verified = "Verified";
  public const string Sanctioned = "Sanctioned";
  public const string Waived = "Waived";
  public const string WrittenOff = "Written off";
  public const string Reversed = "Reversed";
  public const string Finalized = "Finalized";
  public const string Reviewed = "Reviewed";
  public const string SentBack = "Sent back";
  public const string Unapproved = "Approval withdrawn";
  public const string Paid = "Paid";
  public const string Held = "Held";
  public const string Released = "Released";
  public const string Locked = "Locked";
  public const string Closed = "Closed";
  public const string Reopened = "Reopened";
  public const string Processed = "Processed";
  public const string Failed = "Failed";
  public const string Countersigned = "Countersigned";
  public const string Acknowledged = "Acknowledged";
  public const string Linked = "Linked";
  public const string Unlinked = "Unlinked";
  public const string Separated = "Separated";
}
