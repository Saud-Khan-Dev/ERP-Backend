// ---- demand runs and demands ----

public sealed record DemandRunDto(
  Guid Id, int FiscalYear, string FiscalYearLabel, Guid? CircleId, string? CircleCode, Guid RateSetId, string? RateSetName, DemandRunStatus Status,
  DateTime BasisSyncedAt, int? PropertyCount, decimal? TotalAmount, int? ExceptionCount, DateTime? ComputedAt, Guid? ComputedBy, DateTime? ApprovedAt,
  Guid? ApprovedBy, int DraftCount);

public sealed record RunProblemDto(Guid TaxFileId, string FileNo, string Problem);

/// Computed: drafts made or refreshed; Skipped: files whose demand of the year is already issued; Held: files on
/// litigation hold or not assessable; Problems: files that could not be computed and why.
public sealed record ComputeRunResult(Guid RunId, int Computed, int Skipped, int Held, decimal TotalAmount, IReadOnlyList<RunProblemDto> Problems);

/// HeldWithoutNotice: demands issued on files under a litigation hold - no first notice went out (the daily job prepares it
/// once the hold ends). Stale: drafts whose file changed after the run was worked out (the amount issued is the draft's;
/// work the run out again first to take the change). CreditsCarried: what earlier payments beyond their charges paid of
/// the new demands.
public sealed record ApproveRunResult(Guid RunId, int Issued, int NoticesIssued, int SmsQueued, int YearEndSurcharges, IReadOnlyList<RunProblemDto> HeldWithoutNotice,
  IReadOnlyList<RunProblemDto> Stale, decimal CreditsCarried);

public sealed record DemandSummaryDto(
  Guid Id, string DemandNo, Guid TaxFileId, string FileNo, string? Psid, Guid? RunId, int FiscalYear, string FiscalYearLabel, string PayerName, string? PayerMobile,
  decimal CurrentAmount, decimal ArrearsBf, decimal PenaltyAmount, decimal PaidAmount, decimal Balance, DemandStatus Status, DemandStage? Stage,
  DateOnly? IssuedOn, DateOnly? DueDate, DateOnly? PaidOn);

public sealed record DemandLineDto(
  Guid Id, Guid HeadId, string HeadCode, string HeadName, decimal? BasisQty, string? BasisUnit, decimal BaseAmount, decimal UpliftAmount, decimal IncreaseAmount,
  decimal OverrideAmount, decimal Amount, LineMode LineMode, Guid? OverrideId, Guid? PolicyId, decimal? NormalAmount, string? Explanation, decimal Paid,
  decimal Outstanding);

/// Correctable: an opening due of the old register not billed yet (it can be corrected, or removed while nothing was paid on it).
public sealed record ArrearsDto(
  Guid Id, Guid HeadId, string HeadCode, string HeadName, int? FiscalYear, string? FiscalYearLabel, decimal OriginalAmount, decimal PenaltyAmount, decimal Outstanding,
  ArrearsSource Source, Guid? SourceDemandId, Guid? AddedToDemandId, string? Description, Guid? EnteredBy, DateTime? EnteredAt, string? Reason, decimal Settled,
  bool Correctable);

public sealed record PenaltyDto(
  Guid Id, Guid? RuleId, Guid? RunId, PenaltyKind Kind, decimal BaseAmount, decimal? Pct, decimal Amount, DateOnly AppliedOn, string? RuleRef, decimal Paid);

public sealed record NoticeBriefDto(Guid Id, string NoticeNo, NoticeStage Stage, NoticeStatus Status, NoticeOrigin Origin, DateOnly IssueDate, DateOnly? ReplyBy,
  decimal AmountWithinDue, DateOnly? ServedOn);

public sealed record DemandDto(
  DemandSummaryDto Summary, Guid BasisId, Guid PayerId, string? PayerCnic, string? PayerAddress, decimal? CarriedAmount, Guid? CarriedToDemandId, decimal Excess,
  IReadOnlyList<DemandLineDto> Lines, IReadOnlyList<ArrearsDto> Arrears, IReadOnlyList<PenaltyDto> Penalties, IReadOnlyList<NoticeBriefDto> Notices,
  DateTime? CreatedAt);

public static class DemandMappings
{
  public static DemandSummaryDto ToSummary(this Demand d, string fileNo, string? psid) => new(
    d.Id.Value, d.DemandNo, d.TaxFileId.Value, fileNo, psid, d.RunId?.Value, d.FiscalYear, FiscalYear.Label(d.FiscalYear), d.PayerName, d.PayerMobile,
    d.CurrentAmount, d.ArrearsBf, d.PenaltyAmount, d.PaidAmount, d.Balance, d.Status, d.Stage, d.IssuedOn, d.DueDate, d.PaidOn);

  public static ArrearsDto ToDto(this Arrears a, IReadOnlyDictionary<TaxHeadId, TaxHead> heads) => new(
    a.Id.Value, a.HeadId.Value, heads.GetValueOrDefault(a.HeadId)?.Code ?? "", heads.GetValueOrDefault(a.HeadId)?.Name ?? "", a.FiscalYear,
    a.FiscalYear is { } fy ? FiscalYear.Label(fy) : null, a.OriginalAmount, a.PenaltyAmount, a.Outstanding, a.Source, a.SourceDemandId?.Value,
    a.AddedToDemandId?.Value, a.Description, a.EnteredBy, a.EnteredAt, a.Reason, a.Settled, a.IsUnbilledOpening);

  public static NoticeBriefDto ToBrief(this Notice n) => new(n.Id.Value, n.NoticeNo, n.Stage, n.Status, n.Origin, n.IssueDate, n.ReplyBy, n.AmountWithinDue, n.ServedOn);

  public static DemandDto ToDto(this Demand d, string fileNo, string? psid, IReadOnlyDictionary<TaxHeadId, TaxHead> heads, IReadOnlyDictionary<Guid, decimal> paid,
      IEnumerable<Notice> notices) => new(
    d.ToSummary(fileNo, psid), d.BasisId.Value, d.PayerId.Value, d.PayerCnic, d.PayerAddress, d.CarriedAmount, d.CarriedToDemandId?.Value, d.Excess,
    d.Lines.Select(l =>
    {
      var head = heads.GetValueOrDefault(l.HeadId);
      var settled = paid.GetValueOrDefault(l.Id.Value);
      return new DemandLineDto(l.Id.Value, l.HeadId.Value, head?.Code ?? "", head?.Name ?? "", l.BasisQty, l.BasisUnit, l.BaseAmount, l.UpliftAmount, l.IncreaseAmount,
        l.OverrideAmount, l.Amount, l.LineMode, l.OverrideId?.Value, l.PolicyId?.Value, l.NormalAmount, l.Explanation, settled,
        d.Status == DemandStatus.Carried ? 0 : Math.Max(0, l.Amount - settled));
    }).ToList(),
    d.ArrearsRows.OrderBy(a => a.FiscalYear ?? 0).Select(a => a.ToDto(heads)).ToList(),
    d.Penalties.OrderBy(p => p.AppliedOn).Select(p => new PenaltyDto(p.Id.Value, p.RuleId?.Value, p.RunId?.Value, p.Kind, p.BaseAmount, p.Pct, p.Amount, p.AppliedOn,
      p.RuleRef, paid.GetValueOrDefault(p.Id.Value))).ToList(),
    notices.OrderBy(n => n.IssueDate).ThenBy(n => n.CreatedAt).Select(n => n.ToBrief()).ToList(),
    d.CreatedAt);
}

// ---- penalties ----

public sealed record PenaltyRunDto(
  Guid Id, Guid RuleId, string? RuleCode, string? RuleName, int FiscalYear, string FiscalYearLabel, Guid? CircleId, string? CircleCode, DateOnly RunOn, TargetSet TargetSet,
  PenaltyRateType RateType, decimal Rate, bool IncludesCurrent, string? Reference, int? PropertyCount, decimal? TotalAmount, Guid? ApprovedBy, DateTime? CreatedAt);

/// ArrearsOwed: what the earlier years' rows billed on it still owe (a defaulter owes some). SurchargedOn: the day an
/// earlier run of the rule surcharged it (it is not surcharged twice).
public sealed record SurchargeCandidateDto(
  Guid DemandId, string DemandNo, Guid TaxFileId, string FileNo, string PayerName, decimal Balance, decimal ArrearsOwed, decimal Base, decimal Amount, bool Eligible,
  string? Reason, DateOnly? SurchargedOn);

/// A surcharge line, for the Surcharges report and a run's page.
public sealed record PenaltyListItemDto(
  Guid Id, Guid DemandId, string DemandNo, Guid TaxFileId, string FileNo, string PayerName, Guid? CircleId, string? CircleCode, int FiscalYear, string FiscalYearLabel,
  PenaltyKind Kind, decimal BaseAmount, decimal? Pct, decimal Amount, DateOnly AppliedOn, Guid? RunId, string? RuleRef, decimal Paid);

public sealed record SurchargePreviewDto(int Eligible, decimal Total, IReadOnlyList<SurchargeCandidateDto> Candidates);
