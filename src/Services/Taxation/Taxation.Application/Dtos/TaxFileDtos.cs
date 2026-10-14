// ---- tax files ----

/// Dues: what is behind the reference today.
public sealed record TaxFileListItemDto(
  Guid Id, string FileNo, Guid PropertyId, string PropertyCode, Guid CircleId, string? CircleCode, Guid TownId, TaxCategory TaxCategory, HoldingKind HoldingKind,
  string? Psid, string? Addressee, TaxStatus TaxStatus, bool OnHold, decimal Dues, DateOnly OpenedOn);

public sealed record BasisDto(
  Guid Id, decimal? LandAreaSqyd, decimal? LandAreaEntered, string? LandAreaUnitCode, decimal? LandAreaKanal, decimal? RegularizedAreaSqyd, decimal? TaxableLandSqyd,
  decimal? TaxableKanal, decimal? CoveredAreaSqft, CoveredAreaSource? CoveredAreaSource, Guid? SourceMeasurementId, Guid? SourceBuildingPlanId, DateOnly? BpaDate,
  Guid? CompletionPlanId, DateOnly? CompletionDate, Guid? PropertyTypeId, string? PropertyStatusCode, string? ClassificationCode, DateTime SyncedAt, bool IsCurrent);

public sealed record PayerDto(
  Guid Id, Guid OwnerId, string OwnerCode, PayerRole PayerRole, PayerSource SourceType, Guid SourceId, decimal? SharePct, bool IsAddressee, string Name,
  string? FatherHusbandName, string? CnicNtn, string? Mobile, string? PostalAddress, DateOnly EffectiveFrom, DateOnly? EffectiveTo, DateTime SyncedAt);

public sealed record HeadValueDto(Guid Id, Guid HeadId, string HeadCode, string HeadName, DateOnly? FacilityFrom, decimal? Quantity);

public sealed record OverrideDto(
  Guid Id, Guid HeadId, string HeadCode, string HeadName, OverrideMode Mode, Guid? PolicyId, string? PolicyName, decimal? Amount, bool FollowsIncrease, string Reason,
  string? Reference, DateOnly EffectiveFrom, DateOnly? EffectiveTo, Guid? ApprovedBy, DateTime? ApprovedAt, bool IsCurrent);

public sealed record HoldDto(Guid Id, string Reason, string? Reference, Guid? SourceLitigationId, DateOnly StartedOn, DateOnly? EndedOn, bool SuspendsDemand, bool ExcludesCompletionClock);

public sealed record StepRevisionDto(Guid Id, DateOnly EffectiveFrom, decimal StepPct, int StepIntervalYears, string? BoardReference);

public sealed record AgreementDto(
  Guid Id, AgreementKind Kind, Guid SourceId, string SourceNo, decimal BaseAmount, AmountFrequency Frequency, decimal AnnualBase, DateOnly StartDate, DateOnly? EndDate,
  int? TermYears, decimal? StepPct, int? StepIntervalYears, bool IsDirect99Year, DateOnly? NextRenewalDate, AgreementStatus Status,
  IReadOnlyList<StepRevisionDto> Revisions);

public sealed record ReferenceSyncDto(
  Guid Id, Guid TaxFileId, string Psid, int SyncNo, PsidSyncReason Reason, Guid? DemandId, Guid? PenaltyId, decimal AmountBefore, decimal AmountAfter,
  DateOnly? DueDate, DateTime SyncedAt, string? OnelinkAck, string? Note, PsidSyncStatus Status);

public sealed record TaxFileDto(
  Guid Id, string FileNo, Guid PropertyId, string PropertyCode, Guid CircleId, string? CircleCode, Guid TownId, string? TownLabel, TaxCategory TaxCategory,
  HoldingKind HoldingKind, int? Rooms, string? Psid, DateOnly? RegisteredOn, DateOnly? WaterFacilityFrom, UpliftRateBasis? UpliftRateBasis, decimal? UpliftPct,
  string? OldReferenceNo, string? PlotNo, TaxStatus TaxStatus, string? ExemptionReason, DateOnly OpenedOn, DateOnly? ClosedOn, string? Remarks, decimal Dues,
  BasisDto? CurrentBasis, IReadOnlyList<PayerDto> Payers, IReadOnlyList<HeadValueDto> HeadValues, IReadOnlyList<OverrideDto> Overrides,
  IReadOnlyList<HoldDto> Holds, IReadOnlyList<AgreementDto> Agreements, ReferenceSyncDto? LastSync, DateTime? CreatedAt, Guid? CreatedBy);

public static class TaxFileMappings
{
  public static BasisDto ToDto(this TaxFileBasis b) => new(
    b.Id.Value, b.LandAreaSqyd, b.LandAreaEntered, b.LandAreaUnitCode, b.LandAreaKanal, b.RegularizedAreaSqyd, b.TaxableLandSqyd, b.TaxableKanal, b.CoveredAreaSqft,
    b.CoveredAreaSource, b.SourceMeasurementId, b.SourceBuildingPlanId, b.BpaDate, b.CompletionPlanId, b.CompletionDate, b.PropertyTypeId, b.PropertyStatusCode,
    b.ClassificationCode, b.SyncedAt, b.IsCurrent);

  public static PayerDto ToDto(this TaxFilePayer p) => new(
    p.Id.Value, p.OwnerId, p.OwnerCode, p.PayerRole, p.SourceType, p.SourceId, p.SharePct, p.IsAddressee, p.Name, p.FatherHusbandName, p.CnicNtn, p.Mobile,
    p.PostalAddress, p.EffectiveFrom, p.EffectiveTo, p.SyncedAt);

  public static AgreementDto ToDto(this AgreementSchedule a) => new(
    a.Id.Value, a.Kind, a.SourceId, a.SourceNo, a.BaseAmount, a.Frequency, a.AnnualBase, a.StartDate, a.EndDate, a.TermYears, a.StepPct, a.StepIntervalYears,
    a.IsDirect99Year, a.NextRenewalDate, a.Status,
    a.Revisions.OrderBy(r => r.EffectiveFrom).Select(r => new StepRevisionDto(r.Id.Value, r.EffectiveFrom, r.StepPct, r.StepIntervalYears, r.BoardReference)).ToList());

  public static HoldDto ToDto(this LitigationHold h) => new(h.Id.Value, h.Reason, h.Reference, h.SourceLitigationId, h.StartedOn, h.EndedOn, h.SuspendsDemand, h.ExcludesCompletionClock);

  public static ReferenceSyncDto ToDto(this PsidSync s) => new(
    s.Id.Value, s.TaxFileId.Value, s.Psid, s.SyncNo, s.Reason, s.DemandId?.Value, s.PenaltyId?.Value, s.AmountBefore, s.AmountAfter, s.DueDate, s.SyncedAt,
    s.OnelinkAck, s.Note, s.Status);

  public static TaxFileDto ToDto(this TaxFile f, string? circleCode, string? townLabel, decimal dues, IReadOnlyDictionary<TaxHeadId, TaxHead> heads,
      IReadOnlyDictionary<SpecialPolicyId, string> policies, PsidSync? lastSync, DateOnly today)
  {
    string Code(TaxHeadId id) => heads.GetValueOrDefault(id)?.Code ?? "";
    string Name(TaxHeadId id) => heads.GetValueOrDefault(id)?.Name ?? "";
    return new TaxFileDto(
      f.Id.Value, f.FileNo, f.PropertyId, f.PropertyCode, f.CircleId.Value, circleCode, f.TownId, townLabel, f.TaxCategory, f.HoldingKind, f.Rooms, f.Psid,
      f.RegisteredOn, f.WaterFacilityFrom, f.UpliftRateBasis, f.UpliftPct, f.OldReferenceNo, f.PlotNo, f.TaxStatus, f.ExemptionReason, f.OpenedOn, f.ClosedOn,
      f.Remarks, dues, f.CurrentBasis?.ToDto(),
      f.Payers.OrderBy(p => p.EffectiveTo is null ? 0 : 1).ThenByDescending(p => p.IsAddressee).ThenByDescending(p => p.SharePct).ThenBy(p => p.Name).Select(p => p.ToDto()).ToList(),
      f.HeadValues.Select(v => new HeadValueDto(v.Id.Value, v.HeadId.Value, Code(v.HeadId), Name(v.HeadId), v.FacilityFrom, v.Quantity)).ToList(),
      f.Overrides.OrderBy(o => Code(o.HeadId)).ThenByDescending(o => o.EffectiveFrom)
        .Select(o => new OverrideDto(o.Id.Value, o.HeadId.Value, Code(o.HeadId), Name(o.HeadId), o.Mode, o.PolicyId?.Value,
          o.PolicyId is { } pid ? policies.GetValueOrDefault(pid) : null, o.Amount, o.FollowsIncrease, o.Reason, o.Reference, o.EffectiveFrom, o.EffectiveTo,
          o.ApprovedBy, o.ApprovedAt, o.Covers(today))).ToList(),
      f.Holds.OrderByDescending(h => h.StartedOn).Select(h => h.ToDto()).ToList(),
      f.Agreements.OrderByDescending(a => a.StartDate).Select(a => a.ToDto()).ToList(),
      lastSync?.ToDto(), f.CreatedAt, f.CreatedBy);
  }
}

// ---- assessment preview ----

public sealed record ComputedLineDto(
  Guid HeadId, string HeadCode, string HeadName, decimal? BasisQty, string? BasisUnit, decimal BaseAmount, decimal UpliftAmount, decimal IncreaseAmount,
  decimal OverrideAmount, decimal Amount, LineMode Mode, Guid? OverrideId, Guid? PolicyId, decimal NormalAmount, string Explanation);

public sealed record AssessmentDto(
  Guid TaxFileId, string FileNo, int FiscalYear, string FiscalYearLabel, Guid RateSetId, string RateSetName, bool Suspended, string? SuspendedReason,
  TaxCategory CategoryUsed, decimal UpliftPct, int IncreaseYears, IReadOnlyList<ComputedLineDto> Lines, IReadOnlyList<string> Notes, decimal Current,
  decimal ArrearsPreview, decimal Total);

public static class AssessmentMappings
{
  public static ComputedLineDto ToDto(this ComputedLine l) => new(
    l.HeadId.Value, l.HeadCode, l.HeadName, l.BasisQty, l.BasisUnit, l.BaseAmount, l.UpliftAmount, l.IncreaseAmount, l.OverrideAmount, l.Amount, l.Mode,
    l.OverrideId?.Value, l.PolicyId?.Value, l.NormalAmount, l.Explanation);
}
