/// taxation.special_policy: the "special" column of a head, set by the admin. Instead of the normal formula a head is
/// charged a fixed yearly amount, an amount a month, a percentage of the normal tax, or a rate per room, sq ft or kanal.
/// A property gets one through an override; a notice can switch each head between Normal and Special.
public class SpecialPolicy : Aggregate<SpecialPolicyId>
{
  public string? Code { get; private set; }
  public string Name { get; private set; } = default!;
  public TaxHeadId HeadId { get; private set; } = default!;
  public PolicyMethod Method { get; private set; }
  public decimal Value { get; private set; }
  public AppliesTo AppliesTo { get; private set; }
  public bool AnnualIncrease { get; private set; }
  public DateOnly EffectiveFrom { get; private set; }
  public DateOnly? EffectiveTo { get; private set; }
  public string? BoardReference { get; private set; }
  public bool IsActive { get; private set; }

  private SpecialPolicy() { }

  public static SpecialPolicy Create(SpecialPolicyId id, TaxHead head, SpecialPolicyInput input)
  {
    var policy = new SpecialPolicy { Id = id };
    policy.Update(head, input);
    return policy;
  }

  public void Update(TaxHead head, SpecialPolicyInput input)
  {
    Code = Guard.OptionalCode(input.Code, 30, "Policy code");
    Name = Guard.RequiredText(input.Name, 150, "Policy name");
    if (!head.OverrideAllowed)
      throw new DomainException($"{head.Name} does not allow a special charge.");
    if (head.HeadKind != HeadKind.Regular)
      throw new DomainException($"{head.Name} is not part of the annual demand.");
    HeadId = head.Id;
    Method = input.Method;
    Value = Guard.Positive(input.Value, "Value");
    if (Method == PolicyMethod.PercentOfNormal && Value > 1000)
      throw new DomainException("A percentage of the normal tax cannot exceed 1000 %.");
    AppliesTo = input.AppliesTo;
    AnnualIncrease = Method != PolicyMethod.PercentOfNormal && input.AnnualIncrease;
    Guard.DateOrder(input.EffectiveFrom, input.EffectiveTo, "Effective from", "Effective to");
    EffectiveFrom = input.EffectiveFrom;
    EffectiveTo = input.EffectiveTo;
    BoardReference = Guard.Text(input.BoardReference, 100, "Board reference");
    IsActive = input.IsActive;
  }

  public void SetActive(bool isActive) => IsActive = isActive;

  public bool AppliesToCategory(TaxCategory category) =>
      AppliesTo == AppliesTo.Both || (AppliesTo == AppliesTo.Com && category == TaxCategory.Com) || (AppliesTo == AppliesTo.Res && category == TaxCategory.Res);

  /// In force for a year: active, for the category, and covering 1 July of the year.
  public bool AvailableFor(TaxHeadId headId, TaxCategory category, int fiscalYear)
  {
    var start = FiscalYear.Start(fiscalYear);
    return IsActive && HeadId == headId && AppliesToCategory(category) && EffectiveFrom <= start && (EffectiveTo is null || EffectiveTo >= start);
  }
}

public sealed record SpecialPolicyInput(
  string? Code,
  string Name,
  PolicyMethod Method,
  decimal Value,
  AppliesTo AppliesTo,
  bool AnnualIncrease,
  DateOnly EffectiveFrom,
  DateOnly? EffectiveTo,
  string? BoardReference,
  bool IsActive);

/// taxation.penalty_rule: a Board-controlled penalty. INTRA_YEAR is the discretionary surcharge the Section applies
/// after a period; YEAR_END is the automatic percentage on whatever is outstanding once 30 June has passed.
public class PenaltyRule : Aggregate<PenaltyRuleId>
{
  public string Code { get; private set; } = default!;
  public string Name { get; private set; } = default!;
  public PenaltyKind Kind { get; private set; }
  public PenaltyTrigger Trigger { get; private set; }
  public int? PeriodValue { get; private set; }
  public PeriodUnit? PeriodUnit { get; private set; }
  public PeriodAnchor? PeriodAnchor { get; private set; }
  public PenaltyRateType RateType { get; private set; }
  public decimal Rate { get; private set; }
  public PenaltyBase Base { get; private set; }
  public TargetSet TargetSet { get; private set; }
  public bool IncludesCurrent { get; private set; }
  public bool? StacksWithYearEnd { get; private set; }
  public RateSetId? RateSetId { get; private set; }
  public DateOnly EffectiveFrom { get; private set; }
  public DateOnly? EffectiveTo { get; private set; }

  private PenaltyRule() { }

  public static PenaltyRule Create(PenaltyRuleId id, string code, string name, PenaltyKind kind, PenaltyTrigger trigger, PenaltyRuleInput input, DateOnly effectiveFrom)
  {
    var rule = new PenaltyRule { Id = id, Code = Guard.Code(code, 30, "Rule code"), Kind = kind, Trigger = trigger, Base = PenaltyBase.Outstanding, EffectiveFrom = effectiveFrom };
    rule.Name = Guard.RequiredText(name, 100, "Rule name");
    rule.Update(input);
    return rule;
  }

  public void Update(PenaltyRuleInput input)
  {
    Name = Guard.RequiredText(input.Name ?? Name, 100, "Rule name");
    RateType = input.RateType;
    Rate = input.RateType == PenaltyRateType.Percent ? Guard.Between(input.Rate, 0.01m, 100m, "Rate %") : Guard.Positive(input.Rate, "Fixed amount");
    TargetSet = input.TargetSet;
    IncludesCurrent = input.IncludesCurrent;
    StacksWithYearEnd = input.StacksWithYearEnd;
    RateSetId = input.RateSetId is { } rateSet ? global::RateSetId.Of(rateSet) : null;

    if (Kind == PenaltyKind.IntraYear)
    {
      PeriodValue = Guard.Between(input.PeriodValue ?? throw new DomainException("Give the period after which the surcharge may be applied."), 0, 366, "Period");
      PeriodUnit = input.PeriodUnit ?? global::PeriodUnit.Months;
      PeriodAnchor = input.PeriodAnchor ?? global::PeriodAnchor.FyStart;
    }
    else
    {
      PeriodValue = null;
      PeriodUnit = null;
      PeriodAnchor = null;
    }
  }

  /// The surcharge on a base: a percentage of it or the fixed amount.
  public decimal AmountOn(decimal baseAmount) => RateType == PenaltyRateType.Percent ? Rupees.Round(baseAmount * Rate / 100m) : Rupees.Round(Rate);

  /// The first day an in-year surcharge may be applied to a demand.
  public DateOnly EligibleFrom(int fiscalYear, DateOnly? issuedOn, DateOnly dueDate)
  {
    var anchor = PeriodAnchor switch
    {
      global::PeriodAnchor.DemandIssue => issuedOn ?? FiscalYear.Start(fiscalYear),
      global::PeriodAnchor.DueDate => dueDate,
      _ => FiscalYear.Start(fiscalYear)
    };
    var value = PeriodValue ?? 0;
    return PeriodUnit == global::PeriodUnit.Days ? anchor.AddDays(value) : anchor.AddMonths(value);
  }
}

public sealed record PenaltyRuleInput(
  string? Name,
  PenaltyRateType RateType,
  decimal Rate,
  TargetSet TargetSet,
  bool IncludesCurrent,
  bool? StacksWithYearEnd,
  int? PeriodValue,
  PeriodUnit? PeriodUnit,
  PeriodAnchor? PeriodAnchor,
  Guid? RateSetId);

/// taxation.message_template: the SMS texts (and print fragments) by code. Placeholders in braces are filled when sent.
public class MessageTemplate : Aggregate<MessageTemplateId>
{
  public string Code { get; private set; } = default!;
  public MessageChannel Channel { get; private set; }
  public string Language { get; private set; } = default!;
  public string Body { get; private set; } = default!;
  public int VersionNo { get; private set; }
  public bool IsActive { get; private set; }

  private MessageTemplate() { }

  public static MessageTemplate Create(MessageTemplateId id, string code, MessageChannel channel, string body, string language = "en") => new()
  {
    Id = id,
    Code = Guard.Code(code, 40, "Template code"),
    Channel = channel,
    Language = Guard.RequiredText(language, 5, "Language").ToLowerInvariant(),
    Body = Guard.RequiredText(body, 1000, "Text"),
    VersionNo = 1,
    IsActive = true
  };

  public void Change(string body, bool isActive)
  {
    var text = Guard.RequiredText(body, 1000, "Text");
    if (text != Body)
    {
      Body = text;
      VersionNo++;
    }
    IsActive = isActive;
  }

  /// The text with its placeholders filled; unknown placeholders stay as written.
  public string Fill(IReadOnlyDictionary<string, string> values) =>
      System.Text.RegularExpressions.Regex.Replace(Body, @"\{(\w+)\}", m => values.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);
}

/// The SMS template codes the module sends.
public static class MessageCodes
{
  public const string NoticeFirst = "NOTICE_FIRST";
  public const string NoticeSecond = "NOTICE_SECOND";
  public const string NoticeFinal = "NOTICE_FINAL";
  public const string ShowCause = "SHOW_CAUSE";
  public const string Penalty = "PENALTY";
  public const string YearEnd = "YEAR_END";
  public const string PaymentReceived = "PAYMENT_RECEIVED";
  public const string Clearance = "CLEARANCE";

  public static string For(NoticeStage stage) => stage switch
  {
    NoticeStage.First => NoticeFirst,
    NoticeStage.Second => NoticeSecond,
    NoticeStage.Final => NoticeFinal,
    NoticeStage.ShowCause => ShowCause,
    _ => Penalty
  };
}
