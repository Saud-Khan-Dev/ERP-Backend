// One C# enum per Postgres enum type of the hrms schema (hrms.<name>_enum). The DB label is the member name in
// snake_case (DeputedOut -> deputed_out); members are declared in the schema's label order, which is the order
// Postgres sorts them in. Adding, renaming or removing a member needs a migration (ALTER TYPE ... ADD VALUE).

/// Transgender = NADRA's third gender ("X" on the CNIC). Other stays in the database type but is never offered.
public enum Gender { Male, Female, Other, Transgender }

public enum MaritalStatus { Single, Married, Divorced, Widowed }

/// The last four are the real end-of-service states a separation leaves (dismissed, removed from service, contract ended,
/// repatriated to the parent department) instead of collapsing them into Terminated.
public enum EmploymentStatus { Active, OnLeave, Suspended, DeputedOut, Retired, Resigned, Terminated, Deceased, Dismissed, Removed, ContractEnded, Repatriated }

/// The rules an employment type follows (HR names the types in Configuration): Regular = permanent, with an employment
/// method, retires; FixedTerm = engaged for a term (contract, contingent, ad hoc, daily wage); Deputation = a permanent
/// employee of another department serving here for a term; Project = paid from a development project for a term.
public enum EmploymentBasis { Regular, FixedTerm, Deputation, Project }


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


public enum PaymentStatus { Pending, Processed, Failed, Returned }

public enum PaymentMethod { BankTransfer, Cheque, Cash }

public enum VerificationStatus { Unverified, Pending, Verified, Rejected }

public enum RecordStatus { Active, Inactive }

public enum TaskPriority { Low, Medium, High, Urgent }

public enum EmployeeTaskStatus { Pending, InProgress, Completed, OnHold, Cancelled }

public enum EmployeeRequestStatus { Pending, Approved, Rejected, Cancelled }

/// What part of their own record an employee asks to change (Employee Portal › My profile).
public enum ProfileChangeArea { Contact, Address, EmergencyContact, FamilyMember, Photo }

public enum ProfileChangeAction { Add, Change, Remove }

public enum ProfileChangeStatus { Pending, Approved, Rejected, Withdrawn }

public enum ContactType { Mobile, Phone, Email, Whatsapp, Other }

public enum AddressType { Permanent, Current, Mailing, Other }


/// Other stays in the database type for old rows but a new separation must name its real kind.
public enum SeparationType { Retirement, Resignation, Termination, Dismissal, Death, EndOfContract, DeputationEnd, Other, CompulsoryRetirement, PrematureRetirement, RemovalFromService }

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
  /// A service event type added in Configuration (the action names it in EventTypeId).
  Other,
  Dismissal,
  RemovalFromService,
  CompulsoryRetirement,
  PrematureRetirement,
  EndOfContract,
  DeputationEnd
}

public enum HrActionStatus { Draft, Pending, Approved, Rejected, Cancelled, Applied }

/// Countersigned: the countersigning officer has added remarks (and may have changed the grading) after the reporting officer.
public enum ReviewStatus { Draft, Submitted, Acknowledged, Finalized, Countersigned }

public enum AttendanceStatus { Present, Absent, Late, HalfDay, OnLeave, OfficialDuty, Holiday, Weekend }

public enum HolidayType { Public, Religious, Provincial, Optional }

public enum LeaveStatus { Pending, Approved, Rejected, Cancelled }

/// Leave ledger transaction. Opening / accrual / carry-forward / usage-reversal credit (+), usage / lapse /
/// encashment debit (-), adjustment either way.
public enum LeaveTransactionType { Opening, Accrual, Usage, UsageReversal, CarryForward, Lapse, Encashment, Adjustment }

/// Pending: an application entered and waiting to be sanctioned by someone else (no installments yet).
public enum LoanStatus { Active, Completed, Cancelled, WrittenOff, Pending }

public enum InstallmentStatus { Pending, Partial, Paid, Waived }

/// GP Fund ledger transaction. Opening / subscription / interest / advance recovery credit (+), advance / withdrawal /
/// final payment debit (-), adjustment either way.
public enum GpFundTransactionType { Opening, Subscription, Interest, Advance, AdvanceRecovery, Withdrawal, FinalPayment, Adjustment }
