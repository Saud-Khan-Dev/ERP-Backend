// ---- reports, overview, daily job, change history ----

public sealed record HeadReportRowDto(
  Guid HeadId, string HeadCode, string HeadName, HeadKind HeadKind, int Lines, decimal Current, decimal OfWhichIncrease, decimal OfWhichTopUps, decimal Arrears,
  decimal Surcharges, decimal Demanded, decimal Recovered, decimal Outstanding);

public sealed record HeadReportDto(int FiscalYear, string FiscalYearLabel, IReadOnlyList<HeadReportRowDto> Rows, decimal IntraYearSurcharges, decimal YearEndSurcharges);

public sealed record CircleTownRowDto(
  Guid? CircleId, string? CircleCode, Guid TownId, string TownCode, string? ShortCode, int Files, int Demands, int Paid, decimal Demanded, decimal Recovered,
  decimal Outstanding, int RecoveryPct);

public sealed record CircleTownReportDto(int FiscalYear, string FiscalYearLabel, IReadOnlyList<CircleTownRowDto> Rows);

public sealed record ArrearsReportRowDto(
  Guid TaxFileId, string FileNo, string? Psid, Guid CircleId, string? CircleCode, string PayerName, IReadOnlyList<string> Years, int YearCount, DateTime? LastPaidAt,
  DemandStage? Stage, decimal Outstanding);

public sealed record ArrearsReportDto(PaginatedResult<ArrearsReportRowDto> Files, decimal Total);

/// Taxes: the dues behind references; Fees: fee receipts. Period: the day (or the first of the month) in Pakistan time.
public sealed record CollectionPeriodDto(DateOnly Period, string Label, int Payments, decimal Taxes, decimal Fees, decimal Total);

public sealed record CollectionChannelDto(PaymentChannel Channel, int Payments, decimal Amount);

public sealed record CollectionByPeriodDto(DateOnly From, DateOnly To, string GroupBy, IReadOnlyList<CollectionPeriodDto> Periods, IReadOnlyList<CollectionChannelDto> ByChannel,
  decimal Taxes, decimal Fees);

public sealed record CategoryCountDto(TaxCategory Category, int Files);

public sealed record FileCountsDto(int Total, int Active, int Exempt, int Closed, int OnHold, IReadOnlyList<CategoryCountDto> ByCategory);

public sealed record DemandCountsDto(int Drafts, int Issued, int Unpaid, int PartPaid, int Paid, int Defaulted, int Carried);

public sealed record HeadAmountDto(Guid HeadId, string HeadCode, string HeadName, decimal Amount);

/// Issued / Pending: notices of the stage issued and waiting for confirmation.
public sealed record StageCountDto(NoticeStage Stage, int Issued, int Pending);

/// The demands of the year by the furthest step taken on them.
public sealed record DemandStageCountDto(DemandStage Stage, int Demands);

public sealed record LargestBalanceDto(Guid TaxFileId, string FileNo, string? PayerName, decimal Outstanding);

public sealed record JobRunDto(Guid Id, string Job, DateOnly RunDate, DateTime StartedAt, DateTime? FinishedAt, JobRunStatus Status, Guid? StartedBy, int YearEndSurcharged,
  int NoticesPrepared, string? Summary, string? Error);

/// The Taxation overview of a year. SurchargeEligibleFrom: when the in-year surcharge may be applied this year;
/// SurchargePeriodPassed: that day has come and no surcharge run of the year exists. LastDailyJob: the daily job's last run.
public sealed record TaxOverviewDto(
  int FiscalYear, string FiscalYearLabel, FileCountsDto Files, DemandCountsDto Demands, decimal Demanded, decimal Recovered, int RecoveryPct, decimal OutstandingYear,
  decimal OutstandingAllYears, IReadOnlyList<CircleCollectionDto> ByCircle, IReadOnlyList<CollectionPeriodDto> ByMonth, IReadOnlyList<HeadAmountDto> ByHead,
  IReadOnlyList<StageCountDto> Notices, IReadOnlyList<DemandStageCountDto> DemandStages, int NoticesPending, int NoticesUnserved, int FeesDue, decimal FeesDueAmount,
  decimal FeesCollected, int ClearancesValid, int ReferenceSyncsWaiting, int SmsQueued, decimal IntraYearSurcharges, decimal YearEndSurcharges,
  IReadOnlyList<LargestBalanceDto> LargestBalances, DateOnly? SurchargeEligibleFrom, bool SurchargeRunThisYear, bool SurchargePeriodPassed, JobRunDto? LastDailyJob);

/// One change: Entity is the table (tax_head, tax_file ...); OldValues / NewValues hold the changed fields as JSON.
public sealed record AuditLogDto(Guid Id, string Entity, Guid EntityId, string Action, string? OldValues, string? NewValues, Guid? UserId, string? UserName, DateTime At,
  Guid? TaxFileId);

public static class JobMappings
{
  public static JobRunDto ToDto(this JobRun j) => new(j.Id.Value, j.Job, j.RunDate, j.StartedAt, j.FinishedAt, j.Status, j.StartedBy, j.YearEndSurcharged,
    j.NoticesPrepared, j.Summary, j.Error);
}
