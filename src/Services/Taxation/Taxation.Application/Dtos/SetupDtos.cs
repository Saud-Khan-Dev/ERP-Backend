/// The id of what a command created.
public sealed record CreatedResult(Guid Id);

/// A command that changed something and has nothing else to say.
public sealed record UpdatedResult(bool IsSuccess)
{
  public static readonly UpdatedResult Done = new(true);
}

public static class CommandResults
{
  public static Result<CreatedResult> Created(ITypedId id) => Result<CreatedResult>.Success(new CreatedResult(id.Value));

  public static Result<UpdatedResult> Updated() => Result<UpdatedResult>.Success(UpdatedResult.Done);
}

// ---- organisation ----

public sealed record CircleTownDto(Guid Id, Guid TownId, string TownCode, string? ShortCode, DateOnly EffectiveFrom, DateOnly? EffectiveTo, bool IsCurrent);

public sealed record CircleStaffDto(Guid Id, Guid? CircleId, Guid PostId, TaxRole TaxRole, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

/// SigningPostId: the circle's Taxation Officer post on the day (its tax_circle_staff row); its holder signs the notices.
public sealed record CircleDto(
  Guid Id, string Code, string Name, Guid? OrgUnitId, string? OfficeAddress, string? OfficeFax, string? OfficePhone,
  int SortOrder, bool IsActive, IReadOnlyList<CircleTownDto> Towns, IReadOnlyList<CircleStaffDto> Staff, Guid? SigningPostId, bool InUse);

// ---- heads, categories, rates ----

public sealed record HeadDto(
  Guid Id, string Code, string Name, HeadKind HeadKind, CalcMethod? CalcMethod, bool IsBuiltin, bool IsSystem, AppliesTo AppliesTo, ChargedFrom ChargedFrom,
  string? FacilityLabel, string? FormulaCom, string? FormulaRes, bool AppliesAnnualIncrease, bool OverrideAllowed, bool IsRefundable, string? AccountsHeadCode,
  int SortOrder, bool IsActive, string? BoardReference, DateOnly? EffectiveFrom, bool ChargedOnNotice, bool OnNotice);

/// What a head being defined would charge a file for a year (Amount 0 with the notes saying why when it charges nothing).
public sealed record HeadPreviewDto(
  Guid TaxFileId, string FileNo, int FiscalYear, string FiscalYearLabel, string RateSetName, bool Suspended, string? SuspendedReason, decimal Amount,
  string? Explanation, decimal? Quantity, string? Unit, IReadOnlyList<string> Notes);

public sealed record CategoryMapDto(Guid Id, Guid PropertyTypeId, string PropertyTypeCode, TaxCategory TaxCategory);

public sealed record RateSetSummaryDto(
  Guid Id, string Name, string BoardReference, DateOnly? BoardDecisionDate, DateOnly EffectiveFrom, DateOnly? EffectiveTo, RateSetStatus Status,
  Guid? ApprovedBy, DateTime? ApprovedAt, int BaseFiscalYear, int FactorCount, int RateCount);

public sealed record BandDto(Guid Id, decimal From, decimal? UpTo, decimal Value);

public sealed record LtfTableDto(Guid TownId, string? TownCode, TaxCategory TaxCategory, LtfComponent Component, IReadOnlyList<BandDto> Bands);

public sealed record RateRowDto(Guid Id, RateBasis Basis, string? UnitLabel, bool RoundUp, decimal? BlockSize, decimal? BandFrom, decimal? BandTo, decimal Rate, RateType RateType,
  bool ProRata, AboveTopRule? AboveTopRule);

public sealed record HeadRatesDto(Guid HeadId, string HeadCode, string HeadName, CalcMethod? CalcMethod, TaxCategory? TaxCategory, Guid? TownId, string? TownCode, IReadOnlyList<RateRowDto> Rows);

public sealed record RateSetDto(RateSetSummaryDto Summary, IReadOnlyList<LtfTableDto> LocalityFactors, IReadOnlyList<HeadRatesDto> HeadRates);

public sealed record SettingDto(string Key, string Value, string Default, string Description, DateOnly? EffectiveFrom, DateOnly? EffectiveTo, Guid? RateSetId);

/// UsedBy: the files it is assigned to now (through an override).
public sealed record SpecialPolicyDto(
  Guid Id, string? Code, string Name, Guid HeadId, string HeadCode, string HeadName, PolicyMethod Method, decimal Value, AppliesTo AppliesTo, bool AnnualIncrease,
  DateOnly EffectiveFrom, DateOnly? EffectiveTo, string? BoardReference, bool IsActive, int UsedBy);

/// InForce: the version in force today; HasLater: a later version follows it (it changes from that date).
public sealed record PenaltyRuleDto(
  Guid Id, string Code, string Name, PenaltyKind Kind, PenaltyTrigger Trigger, int? PeriodValue, PeriodUnit? PeriodUnit, PeriodAnchor? PeriodAnchor,
  PenaltyRateType RateType, decimal Rate, PenaltyBase Base, TargetSet TargetSet, bool IncludesCurrent, bool? StacksWithYearEnd, Guid? RateSetId,
  DateOnly EffectiveFrom, DateOnly? EffectiveTo, bool InForce, bool HasLater);

public sealed record MessageTemplateDto(Guid Id, string Code, MessageChannel Channel, string Language, string Body, int VersionNo, bool IsActive);

public sealed record NoticeTemplateDto(
  Guid Id, NoticeStage Stage, string Name, string TitleText, string ParagraphText, string? SourceDocument, int VersionNo, DateOnly EffectiveFrom, bool IsActive,
  IReadOnlyList<string> Placeholders);

public sealed record LetterheadDto(
  Guid Id, string GovernmentLine, string AuthorityLine, string? Motto, IReadOnlyList<string> Notes, IReadOnlyList<string> CcLines, string? QrText,
  string? QrBankName, bool HasLogoLeft, bool HasLogoRight, bool Watermark, string BoardReference, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

public sealed record TableRowDto(Guid Id, string Label, int SortOrder, IReadOnlyList<string> HeadCodes);

public sealed record TableColumnDto(Guid Id, TableColumnKey Key, string Heading, bool PrintedByDefault, int SortOrder);

public sealed record TableLayoutDto(
  Guid Id, bool HideZeroRows, string BoardReference, DateOnly EffectiveFrom, DateOnly? EffectiveTo,
  IReadOnlyList<TableRowDto> Rows, IReadOnlyList<TableColumnDto> Columns);

public static class SetupMappings
{
  public static CircleDto ToDto(this TaxCircle c, IEnumerable<TaxCircleStaff> staff, DateOnly today, bool inUse = true)
  {
    var rows = staff.ToList();
    return new(
      c.Id.Value, c.Code, c.Name, c.OrgUnitId, c.OfficeAddress, c.OfficeFax, c.OfficePhone, c.SortOrder, c.IsActive,
      c.Towns.OrderBy(t => t.TownCode).ThenByDescending(t => t.EffectiveFrom)
        .Select(t => new CircleTownDto(t.Id.Value, t.TownId, t.TownCode, t.ShortCode, t.EffectiveFrom, t.EffectiveTo, t.Covers(today))).ToList(),
      rows.Select(s => s.ToDto()).ToList(),
      rows.Where(s => s.TaxRole == TaxRole.To && s.Covers(today)).OrderByDescending(s => s.EffectiveFrom).Select(s => (Guid?)s.PostId).FirstOrDefault(),
      inUse);
  }

  public static CircleStaffDto ToDto(this TaxCircleStaff s) => new(s.Id.Value, s.CircleId?.Value, s.PostId, s.TaxRole, s.EffectiveFrom, s.EffectiveTo);

  public static HeadDto ToDto(this TaxHead h) => new(
    h.Id.Value, h.Code, h.Name, h.HeadKind, h.CalcMethod, h.IsBuiltin, h.IsSystem, h.AppliesTo, h.ChargedFrom, h.FacilityLabel, h.FormulaCom, h.FormulaRes,
    h.AppliesAnnualIncrease, h.OverrideAllowed, h.IsRefundable, h.AccountsHeadCode, h.SortOrder, h.IsActive, h.BoardReference, h.EffectiveFrom, h.ChargedOnNotice, h.OnNotice);

  public static RateSetSummaryDto ToSummary(this RateSet s) => new(
    s.Id.Value, s.Name, s.BoardReference, s.BoardDecisionDate, s.EffectiveFrom, s.EffectiveTo, s.Status, s.ApprovedBy, s.ApprovedAt, s.BaseFiscalYear,
    s.LtfFactors.Count, s.RateBands.Count);

  public static RateSetDto ToDto(this RateSet s, IReadOnlyDictionary<TaxHeadId, TaxHead> heads, IReadOnlyDictionary<Guid, string> towns)
  {
    var factors = s.LtfFactors.GroupBy(f => (f.TownId, f.TaxCategory, f.Component))
      .OrderBy(g => towns.GetValueOrDefault(g.Key.TownId)).ThenBy(g => g.Key.TaxCategory).ThenBy(g => g.Key.Component)
      .Select(g => new LtfTableDto(g.Key.TownId, towns.GetValueOrDefault(g.Key.TownId), g.Key.TaxCategory, g.Key.Component,
        g.OrderBy(f => f.BandFrom).Select(f => new BandDto(f.Id.Value, f.BandFrom, f.BandTo, f.Factor)).ToList()))
      .ToList();
    var rates = s.RateBands.GroupBy(b => (b.HeadId, b.TaxCategory, b.TownId))
      .Select(g =>
      {
        var head = heads.GetValueOrDefault(g.Key.HeadId);
        return new HeadRatesDto(g.Key.HeadId.Value, head?.Code ?? "", head?.Name ?? "", head?.CalcMethod, g.Key.TaxCategory, g.Key.TownId,
          g.Key.TownId is { } t ? towns.GetValueOrDefault(t) : null,
          g.OrderBy(b => b.BandFrom ?? 0).Select(b => new RateRowDto(b.Id.Value, b.Basis, b.UnitLabel, b.RoundUp, b.BlockSize, b.BandFrom, b.BandTo, b.Rate, b.RateType, b.ProRata, b.AboveTopRule)).ToList());
      })
      .OrderBy(r => heads.GetValueOrDefault(TaxHeadId.Of(r.HeadId))?.SortOrder ?? 999).ThenBy(r => r.HeadName).ThenBy(r => r.TaxCategory).ThenBy(r => r.TownCode)
      .ToList();
    return new RateSetDto(s.ToSummary(), factors, rates);
  }

  public static SpecialPolicyDto ToDto(this SpecialPolicy p, TaxHead? head, int usedBy = 0) => new(
    p.Id.Value, p.Code, p.Name, p.HeadId.Value, head?.Code ?? "", head?.Name ?? "", p.Method, p.Value, p.AppliesTo, p.AnnualIncrease, p.EffectiveFrom, p.EffectiveTo,
    p.BoardReference, p.IsActive, usedBy);

  public static PenaltyRuleDto ToDto(this PenaltyRule r, DateOnly today, bool hasLater = false) => new(
    r.Id.Value, r.Code, r.Name, r.Kind, r.Trigger, r.PeriodValue, r.PeriodUnit, r.PeriodAnchor, r.RateType, r.Rate, r.Base, r.TargetSet, r.IncludesCurrent,
    r.StacksWithYearEnd, r.RateSetId?.Value, r.EffectiveFrom, r.EffectiveTo, r.Covers(today), hasLater);

  public static MessageTemplateDto ToDto(this MessageTemplate t) => new(t.Id.Value, t.Code, t.Channel, t.Language, t.Body, t.VersionNo, t.IsActive);

  public static NoticeTemplateDto ToDto(this NoticeTemplate t) => new(
    t.Id.Value, t.Stage, t.Name, t.TitleText, t.ParagraphText, t.SourceDocument, t.VersionNo, t.EffectiveFrom, t.IsActive, NoticeTemplate.Placeholders);

  public static LetterheadDto ToDto(this NoticeLetterhead l) => new(
    l.Id.Value, l.GovernmentLine, l.AuthorityLine, l.Motto, l.Notes, l.CcLines, l.QrText, l.QrBankName, l.LogoLeftRef is not null, l.LogoRightRef is not null,
    l.Watermark, l.BoardReference, l.EffectiveFrom, l.EffectiveTo);

  public static TableLayoutDto ToDto(this NoticeTableLayout l) => new(
    l.Id.Value, l.HideZeroRows, l.BoardReference, l.EffectiveFrom, l.EffectiveTo,
    l.OrderedRows.Select(r => new TableRowDto(r.Id.Value, r.Label, r.SortOrder, r.Sources.Select(s => s.SourceCode).ToList())).ToList(),
    l.OrderedColumns.Select(c => new TableColumnDto(c.Id.Value, c.ColumnKey, c.Heading, c.PrintedByDefault, c.SortOrder)).ToList());
}
