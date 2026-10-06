// One C# enum per Postgres enum type of the hrms schema (hrms.<name>_enum). The DB label is the member name in
// snake_case (DeputedOut -> deputed_out); members are declared in the schema's label order, which is the order
// Postgres sorts them in. Adding, renaming or removing a member needs a migration (ALTER TYPE ... ADD VALUE).

public enum Gender { Male, Female, Other }

public enum MaritalStatus { Single, Married, Divorced, Widowed }

public enum EmploymentStatus { Active, OnLeave, Suspended, DeputedOut, Retired, Resigned, Terminated, Deceased }

/// Regular = permanent (see EmploymentMethod); DeputationIn = permanent employee of another department serving here;
/// Contract = contingent; ProjectBased; Adhoc; DailyWage.
public enum EmploymentType { Regular, Contract, ProjectBased, DeputationIn, Adhoc, DailyWage }

/// Only for Regular employees: a scheduled (sanctioned-seat) post or a GDA personal post.
public enum EmploymentMethod { ScheduledSeat, GdaPersonal }

/// What is stored on a post version. Occupancy (vacant / partially filled / filled) is derived, never stored.
public enum PostLifecycle { Sanctioned, Frozen, Abolished }

/// Derived post status (v_post_occupancy).
public enum PositionStatus { Sanctioned, Vacant, PartiallyFilled, Filled, Abolished, Frozen }

public enum AssignmentType { Regular, Acting, AdditionalCharge, LookAfter }

public enum ComponentType { Earning, Deduction }

public enum CalculationMethod { Fixed, Percentage, Formula, Tiered }

/// Where a pay-slip line came from.
public enum ComponentSource { Rule, Override, Loan, Gpf, Tax, Adjustment, Manual }

public enum PayrollRunStatus { Draft, Calculated, Reviewed, Approved, Finalized, Paid, Reversed }

public enum PayrollRunType { Regular, Supplementary, Arrears, Bonus, FinalSettlement }

public enum PayrollPeriodStatus { Open, Closed, Locked }

public enum PayrollTransactionStatus { Calculated, Held, Released }

/// Signed amount on the adjustment row: positive raises net pay, negative lowers it.
public enum AdjustmentType { Arrears, Recovery, Bonus, Correction, Other }

public enum PaymentStatus { Pending, Processed, Failed, Returned }

public enum PaymentMethod { BankTransfer, Cheque, Cash }

public enum VerificationStatus { Unverified, Pending, Verified, Rejected }

public enum RecordStatus { Active, Inactive }

public enum TaskPriority { Low, Medium, High, Urgent }

public enum EmployeeTaskStatus { Pending, InProgress, Completed, OnHold, Cancelled }

public enum EmployeeRequestStatus { Pending, Approved, Rejected, Cancelled }

public enum ContactType { Mobile, Phone, Email, Whatsapp, Other }

public enum AddressType { Permanent, Current, Mailing, Other }

public enum QualificationLevel { Matric, Intermediate, Bachelors, Masters, Mphil, Phd, Other }

public enum SeparationType { Retirement, Resignation, Termination, Dismissal, Death, EndOfContract, DeputationEnd, Other }

public enum HrActionType
{
  Appointment,
  Joining,
  Transfer,
  Promotion,
  Demotion,
  DeputationIn,
  DeputationOut,
  Regularization,
  Lwop,
  Suspension,
  Reinstatement,
  Retirement,
  Resignation,
  Termination,
  Death,
  Other
}

public enum HrActionStatus { Draft, Pending, Approved, Rejected, Cancelled, Applied }

public enum ReviewStatus { Draft, Submitted, Acknowledged, Finalized }

public enum AttendanceStatus { Present, Absent, Late, HalfDay, OnLeave, OfficialDuty, Holiday, Weekend }

public enum HolidayType { Public, Religious, Provincial, Optional }

public enum LeaveStatus { Pending, Approved, Rejected, Cancelled }

/// Leave ledger transaction. Opening / accrual / carry-forward / usage-reversal credit (+), usage / lapse /
/// encashment debit (-), adjustment either way.
public enum LeaveTransactionType { Opening, Accrual, Usage, UsageReversal, CarryForward, Lapse, Encashment, Adjustment }

public enum LoanStatus { Active, Completed, Cancelled, WrittenOff }

public enum InstallmentStatus { Pending, Partial, Paid, Waived }

/// GP Fund ledger transaction. Opening / subscription / interest / advance recovery credit (+), advance / withdrawal /
/// final payment debit (-), adjustment either way.
public enum GpFundTransactionType { Opening, Subscription, Interest, Advance, AdvanceRecovery, Withdrawal, FinalPayment, Adjustment }
