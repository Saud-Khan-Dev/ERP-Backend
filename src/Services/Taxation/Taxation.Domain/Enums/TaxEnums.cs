// The fixed value lists of the schema (docs/gda_taxation_module.dbml). Each is stored as UPPER_SNAKE text
// (QrCode -> 'QR_CODE'); the API carries the C# names.

// ---- organisation ----

/// tax_circle_staff.tax_role: Deputy Director (Taxation), Taxation Officer, Assistant Taxation Officer, Tax Collector.
public enum TaxRole
{
  DdTax,
  To,
  Ato,
  Tc
}

// ---- heads and rates ----

/// tax_head.head_kind: REGULAR heads make up the annual demand; OTHER heads are charged case by case (fees, other
/// charges entered on a notice).
public enum HeadKind
{
  Regular,
  Other
}

/// tax_head.calc_method of a regular head. LTF (property tax) and AGREEMENT (rent) are built in.
public enum CalcMethod
{
  Ltf,
  Agreement,
  Bands,
  Rate,
  Fixed,
  Formula
}

/// Which properties a head or a policy applies to.
public enum AppliesTo
{
  Both,
  Res,
  Com
}

/// tax_head.charged_from: every year, from the registration date, or only once a facility is taken (from its date).
public enum ChargedFrom
{
  Always,
  Registration,
  Facility
}

/// The two tax categories the formulas use; EXEMPT properties are not assessed.
public enum TaxCategory
{
  Res,
  Com,
  Exempt
}

public enum RateSetStatus
{
  Draft,
  Approved,
  Superseded
}

/// ltf_factor.component: the land factor (per sq yd) or the covered-area factor (per sq ft).
public enum LtfComponent
{
  Land,
  Covered
}

/// rate_band.basis: the quantity a rate is applied to.
public enum RateBasis
{
  Rooms,
  Kanal,
  Marla,
  Sqft,
  Sqyd,
  Custom,
  PercentOfValue,
  Fixed,
  Page
}

/// rate_band.rate_type: FLAT = the band's amount, PER_UNIT = rate x quantity, PERCENT = % of a value.
public enum RateType
{
  Flat,
  PerUnit,
  Percent
}

/// What the top band of a band table does above its limit (conservancy, open point O-12).
public enum AboveTopRule
{
  TopRateStays,
  RepeatPerBlock
}

public enum MessageChannel
{
  Sms,
  Print
}

// ---- tax file ----

public enum HoldingKind
{
  GdaLease,
  GdaOwnership,
  Rental,
  PrivateRegistered
}

public enum TaxStatus
{
  Active,
  Exempt,
  Sealed,
  Closed
}

/// Which rate the completion-certificate uplift uses.
public enum UpliftRateBasis
{
  Residential,
  Commercial
}

public enum CoveredAreaSource
{
  ApprovedPlan,
  Measurement,
  None
}

public enum PayerRole
{
  Owner,
  Lessee,
  Tenant,
  Allottee
}

public enum PayerSource
{
  Ownership,
  Lease,
  Rental,
  Allotment
}

/// special_policy.method: how a special policy charges a head instead of the normal formula.
public enum PolicyMethod
{
  Fixed,
  PerMonth,
  PercentOfNormal,
  PerRoom,
  PerSqft,
  PerKanal
}

/// tax_file_override.mode: SPECIAL replaces the computed amount (a fixed amount or a special policy); MINIMUM is a floor.
public enum OverrideMode
{
  Special,
  Minimum
}

public enum AgreementKind
{
  Lease,
  Rental
}

public enum AmountFrequency
{
  Annual,
  Monthly,
  Quarterly,
  OneTime
}

public enum AgreementStatus
{
  Active,
  Ended
}

// ---- demand ----

public enum DemandRunStatus
{
  Computed,
  Approved,
  Cancelled
}

public enum DemandStatus
{
  Draft,
  Issued,
  PartPaid,
  Paid,
  Defaulted,
  Carried,
  Cancelled
}

/// demand.stage: the furthest step taken on the demand.
public enum DemandStage
{
  First,
  Second,
  Final,
  ShowCause,
  Penalty,
  Sealed
}

/// demand_line.line_mode: how the line's amount was arrived at.
public enum LineMode
{
  Normal,
  Policy,
  Special,
  Minimum,
  Manual
}

/// arrears.source: an earlier demand's unpaid balance (carried), the old register (imported), or entered on a notice.
public enum ArrearsSource
{
  Demand,
  Import,
  Manual
}

// ---- notices ----

public enum NoticeStage
{
  First,
  Second,
  Final,
  ShowCause,
  Surcharge
}

public enum NoticeOrigin
{
  Cycle,
  Manual
}

/// notice.pay_via: the 1Link / 1Bill reference block, a payment QR code, or both.
public enum PayVia
{
  Onelink,
  Qr,
  Both
}

public enum NoticeStatus
{
  Pending,
  Issued,
  Cancelled
}

public enum ServiceMode
{
  ByHand,
  Post,
  Affixed,
  Refused
}

/// notice_table_column.column_key.
public enum TableColumnKey
{
  Sno,
  Particular,
  Basis,
  Arrears,
  Current,
  Received,
  Payable
}

// ---- penalties ----

public enum PenaltyKind
{
  IntraYear,
  YearEnd,
  S28aFine
}

public enum PenaltyTrigger
{
  Auto,
  Manual
}

public enum PeriodUnit
{
  Months,
  Days
}

public enum PeriodAnchor
{
  FyStart,
  DemandIssue,
  DueDate
}

public enum PenaltyRateType
{
  Percent,
  Fixed
}

public enum PenaltyBase
{
  Outstanding
}

public enum TargetSet
{
  AllUnpaid,
  DefaultersOnly
}

// ---- reference, payments, fees ----

public enum PsidSyncReason
{
  AnnualDemand,
  PenaltyAddUp,
  Override,
  Payment,
  Correction,
  Reversal
}

/// No 1Link connector exists yet: every sync is PENDING until one acknowledges it.
public enum PsidSyncStatus
{
  Pending,
  Acked,
  Failed
}

public enum PaymentChannel
{
  Onelink,
  QrCode,
  Online,
  BankCounter,
  Cash
}

public enum PaymentStatus
{
  Posted,
  Reversed
}

/// What a payment paid: the dues behind a file's reference, or one fee (a fee receipt).
public enum PaymentKind
{
  Demand,
  Fee
}

public enum PartyType
{
  Owner,
  Contractor,
  Vendor,
  Guide,
  Applicant,
  Employee
}

public enum FeeSourceType
{
  Transfer,
  BuildingPlan,
  Allotment,
  Violation,
  WaterConnection,
  Enlistment,
  Licence
}

public enum FeeStatus
{
  Due,
  Paid,
  Waived,
  Refunded
}

public enum ClearancePurpose
{
  Transfer,
  BuildingPlan
}

public enum ClearanceStatus
{
  Issued,
  Consumed,
  Expired,
  Revoked
}

public enum SmsStatus
{
  Queued,
  Sent,
  Delivered,
  Failed
}

// ---- the daily job ----

public enum JobRunStatus
{
  Running,
  Done,
  Failed
}
