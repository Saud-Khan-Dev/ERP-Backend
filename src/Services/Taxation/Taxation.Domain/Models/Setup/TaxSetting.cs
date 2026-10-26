using System.Globalization;

/// taxation.tax_setting: one value of a setting from a date. A change from a date closes the row in force the day before;
/// a change on the same date corrects that row.
public class TaxSetting : Aggregate<TaxSettingId>
{
  public string Key { get; private set; } = default!;
  public string Value { get; private set; } = default!;
  public RateSetId? RateSetId { get; private set; }
  public DateOnly EffectiveFrom { get; private set; }
  public DateOnly? EffectiveTo { get; private set; }

  private TaxSetting() { }

  public static TaxSetting Create(TaxSettingId id, string key, string value, DateOnly effectiveFrom, RateSetId? rateSetId = null)
  {
    var definition = SettingKeys.Find(key);
    return new TaxSetting { Id = id, Key = definition.Key, Value = definition.Normalize(value), EffectiveFrom = effectiveFrom, RateSetId = rateSetId };
  }

  public bool Covers(DateOnly date) => EffectiveFrom <= date && (EffectiveTo is null || EffectiveTo >= date);

  /// The new value from a date. Returns the new row, or null when the row in force was corrected in place.
  public static TaxSetting? Change(TaxSetting? current, string key, string value, DateOnly effectiveFrom, RateSetId? rateSetId, IReadOnlyList<TaxSetting> history)
  {
    var later = history.FirstOrDefault(h => h.EffectiveFrom > effectiveFrom);
    if (later is not null)
      throw new DomainException($"{key} already has a value from {later.EffectiveFrom:yyyy-MM-dd}. Change it from a later date.");

    if (current is not null && current.EffectiveFrom == effectiveFrom)
    {
      current.Value = SettingKeys.Find(key).Normalize(value);
      current.RateSetId = rateSetId ?? current.RateSetId;
      return null;
    }

    current?.Close(effectiveFrom.AddDays(-1));
    return Create(TaxSettingId.New(), key, value, effectiveFrom, rateSetId);
  }

  private void Close(DateOnly lastDay)
  {
    if (lastDay < EffectiveFrom)
      throw new DomainException($"{Key}: the value from {EffectiveFrom:yyyy-MM-dd} cannot end before it starts.");
    EffectiveTo = lastDay;
  }

  /// A value entered from a later date by mistake is taken back before it is in force: the value it would have ended is
  /// in force again. Returns that value (null when there was none).
  public static TaxSetting? Void(TaxSetting future, IReadOnlyList<TaxSetting> history, DateOnly today)
  {
    if (future.EffectiveFrom <= today)
      throw new DomainException($"The value from {future.EffectiveFrom:dd'/'MM'/'yyyy} is already in force; change it from a new date instead.");
    if (history.Any(h => h.Key == future.Key && h.EffectiveFrom > future.EffectiveFrom))
      throw new DomainException($"A later value follows it; take that one back first.");
    var previous = history.Where(h => h.Key == future.Key && h.EffectiveFrom < future.EffectiveFrom).OrderByDescending(h => h.EffectiveFrom).FirstOrDefault();
    if (previous is not null && previous.EffectiveTo == future.EffectiveFrom.AddDays(-1))
      previous.EffectiveTo = null;
    return previous;
  }
}

/// Every setting the module reads, with its default and how a value is checked.
public static class SettingKeys
{
  public const string AnnualIncreasePct = "ANNUAL_INCREASE_PCT";
  public const string NoticeSecondDays = "NOTICE_SECOND_DAYS";
  public const string NoticeFinalDays = "NOTICE_FINAL_DAYS";
  public const string NoticeShowCauseDays = "NOTICE_SHOW_CAUSE_DAYS";
  public const string NoticeDayCounting = "NOTICE_DAY_COUNTING";
  public const string CcGraceYears = "CC_GRACE_YEARS";
  public const string CcUpliftPctRes = "CC_UPLIFT_PCT_RES";
  public const string CcUpliftPctCom = "CC_UPLIFT_PCT_COM";
  public const string CcUpliftRateBasis = "CC_UPLIFT_RATE_BASIS";
  public const string DueDateDdMm = "DUE_DATE_DDMM";
  public const string DueDaysAfterIssue = "DUE_DAYS_AFTER_ISSUE";
  public const string ClearanceValidDays = "CLEARANCE_VALID_DAYS";
  public const string PsidModel = "PSID_MODEL";
  public const string PsidPrefix = "PSID_PREFIX";
  public const string PayViaDefault = "PAY_VIA_DEFAULT";
  public const string QrScheme = "QR_SCHEME";
  public const string QrGuid = "QR_GUID";
  public const string QrMerchantId = "QR_MERCHANT_ID";
  public const string QrMerchantName = "QR_MERCHANT_NAME";
  public const string QrCity = "QR_CITY";
  public const string QrMcc = "QR_MCC";

  public sealed record Definition(string Key, string Default, string Description, Func<string, string> Normalize);

  private static Definition Int(string key, int def, int min, int max, string description) => new(key, def.ToString(CultureInfo.InvariantCulture), description, v =>
      int.TryParse(v.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= min && n <= max
        ? n.ToString(CultureInfo.InvariantCulture)
        : throw new DomainException($"{key} must be a whole number from {min} to {max}."));

  private static Definition Pct(string key, decimal def, decimal max, string description) => new(key, def.ToString(CultureInfo.InvariantCulture), description, v =>
      decimal.TryParse(v.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var n) && n >= 0 && n <= max
        ? n.ToString("0.####", CultureInfo.InvariantCulture)
        : throw new DomainException($"{key} must be a percentage from 0 to {max}."));

  private static Definition OneOf(string key, string def, string description, params string[] values) => new(key, def, description, v =>
      values.FirstOrDefault(x => string.Equals(x, v.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? throw new DomainException($"{key} must be one of: {string.Join(", ", values)}."));

  private static Definition Text(string key, string def, int max, string description, string? pattern = null, string? hint = null) => new(key, def, description, v =>
  {
    var text = v.Trim();
    if (text.Length == 0 || text.Length > max)
      throw new DomainException($"{key} must have 1 to {max} characters.");
    if (pattern is not null && !System.Text.RegularExpressions.Regex.IsMatch(text, pattern))
      throw new DomainException($"{key}: {hint}");
    return text;
  });

  public static readonly IReadOnlyList<Definition> All =
  [
    Pct(AnnualIncreasePct, 10, 100, "Annual increase on the heads that take it, compounded from the rate set's year"),
    Int(NoticeSecondDays, 10, 1, 365, "Second notice: days after the first"),
    Int(NoticeFinalDays, 20, 1, 365, "Final notice: days after the first"),
    Int(NoticeShowCauseDays, 30, 1, 365, "Show cause: days after the first"),
    OneOf(NoticeDayCounting, "WORKING", "Notice days are working days (HRM holiday calendar) or calendar days", "WORKING", "CALENDAR"),
    Int(CcGraceYears, 2, 0, 20, "Years after the building plan before the completion-certificate uplift starts"),
    Pct(CcUpliftPctRes, 100, 1000, "Uplift % of a residential property without a completion certificate"),
    Pct(CcUpliftPctCom, 150, 1000, "Uplift % of a commercial property without a completion certificate"),
    OneOf(CcUpliftRateBasis, "COMMERCIAL", "Which rate the uplift is charged at", "COMMERCIAL", "RESIDENTIAL"),
    Text(DueDateDdMm, "31-08", 5, "Due date of the annual demand in its year (DD-MM)", @"^(0[1-9]|[12]\d|3[01])-(0[1-9]|1[0-2])$", "write the day and month as DD-MM, e.g. 31-08."),
    Int(DueDaysAfterIssue, 30, 0, 365, "A demand issued late is due no sooner than this many days after it is issued"),
    Int(ClearanceValidDays, 30, 1, 365, "Days a clearance certificate stays valid"),
    OneOf(PsidModel, "CONSTANT", "One constant 1Link reference per property, re-synced (a reference per issue is not supported yet)", "CONSTANT"),
    Text(PsidPrefix, "6011", 8, "Prefix of the 1Link / 1Bill consumer references GDA issues", @"^\d{2,8}$", "digits only."),
    OneOf(PayViaDefault, "BOTH", "How a notice is paid unless its creator chooses: ONELINK, QR or BOTH", "ONELINK", "QR", "BOTH"),
    Text(QrScheme, "Raast / bank payment QR (EMVCo)", 100, "The payment QR scheme (label)"),
    Text(QrGuid, "PK.GDA.TAX", 32, "Scheme identifier in the QR", @"^[\x20-\x7E]+$", "plain letters, digits and punctuation."),
    Text(QrMerchantId, "GDA-TAX-0001", 32, "Merchant ID issued by the bank", @"^[\x20-\x7E]+$", "plain letters, digits and punctuation."),
    Text(QrMerchantName, "GALIYAT DEV AUTHORITY", 25, "Merchant name in the QR (max 25)", @"^[\x20-\x7E]+$", "plain letters, digits and punctuation."),
    Text(QrCity, "ABBOTTABAD", 15, "Merchant city in the QR (max 15)", @"^[\x20-\x7E]+$", "plain letters, digits and punctuation."),
    Text(QrMcc, "9311", 4, "Merchant category code", @"^\d{4}$", "four digits.")
  ];

  public static Definition Find(string key) =>
      All.FirstOrDefault(d => string.Equals(d.Key, key?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? throw new DomainException($"'{key}' is not a taxation setting. Use one of: {string.Join(", ", All.Select(d => d.Key))}.");
}

/// The settings in force on a date, typed. Missing values take their defaults.
public sealed record TaxSettings(
  decimal AnnualIncreasePct,
  int NoticeSecondDays,
  int NoticeFinalDays,
  int NoticeShowCauseDays,
  bool CountWorkingDays,
  int CcGraceYears,
  decimal CcUpliftPctRes,
  decimal CcUpliftPctCom,
  UpliftRateBasis CcUpliftRateBasis,
  int DueDay,
  int DueMonth,
  int DueDaysAfterIssue,
  int ClearanceValidDays,
  string PsidPrefix,
  PayVia PayViaDefault,
  string QrScheme,
  string QrGuid,
  string QrMerchantId,
  string QrMerchantName,
  string QrCity,
  string QrMcc)
{
  public static TaxSettings From(IReadOnlyDictionary<string, string> values)
  {
    string V(string key) => values.TryGetValue(key, out var v) ? v : SettingKeys.Find(key).Default;
    int I(string key) => int.Parse(V(key), CultureInfo.InvariantCulture);
    decimal D(string key) => decimal.Parse(V(key), CultureInfo.InvariantCulture);
    var due = V(SettingKeys.DueDateDdMm).Split('-');

    return new TaxSettings(
      D(SettingKeys.AnnualIncreasePct),
      I(SettingKeys.NoticeSecondDays),
      I(SettingKeys.NoticeFinalDays),
      I(SettingKeys.NoticeShowCauseDays),
      V(SettingKeys.NoticeDayCounting) == "WORKING",
      I(SettingKeys.CcGraceYears),
      D(SettingKeys.CcUpliftPctRes),
      D(SettingKeys.CcUpliftPctCom),
      V(SettingKeys.CcUpliftRateBasis) == "RESIDENTIAL" ? UpliftRateBasis.Residential : UpliftRateBasis.Commercial,
      int.Parse(due[0], CultureInfo.InvariantCulture),
      int.Parse(due[1], CultureInfo.InvariantCulture),
      I(SettingKeys.DueDaysAfterIssue),
      I(SettingKeys.ClearanceValidDays),
      V(SettingKeys.PsidPrefix),
      EnumText.Parse<PayVia>(V(SettingKeys.PayViaDefault)),
      V(SettingKeys.QrScheme),
      V(SettingKeys.QrGuid),
      V(SettingKeys.QrMerchantId),
      V(SettingKeys.QrMerchantName),
      V(SettingKeys.QrCity),
      V(SettingKeys.QrMcc));
  }

  /// Rules across settings: the notice days rise (second, then final, then show cause).
  public void EnsureConsistent()
  {
    if (!(NoticeSecondDays < NoticeFinalDays && NoticeFinalDays < NoticeShowCauseDays))
      throw new DomainException("The notice days must rise: second < final < show cause (each counted from the first notice).");
  }

  /// The year's due date: the day and month of DUE_DATE_DDMM in the fiscal year (31 Aug 2026 for FY 2026-27).
  public DateOnly DueDate(int fiscalYear)
  {
    var year = DueMonth >= 7 ? fiscalYear : fiscalYear + 1;
    var day = Math.Min(DueDay, DateTime.DaysInMonth(year, DueMonth));
    return new DateOnly(year, DueMonth, day);
  }

  /// When a demand issued on a day is due: the year's due date, or DUE_DAYS_AFTER_ISSUE days after it is issued when it
  /// is issued late - never before it is issued.
  public DateOnly DueDate(int fiscalYear, DateOnly issuedOn)
  {
    var yearDue = DueDate(fiscalYear);
    var late = issuedOn.AddDays(DueDaysAfterIssue);
    return late > yearDue ? late : yearDue;
  }

  /// Days given to reply to a notice of a stage: up to the next stage, or 10 for the last ones.
  public int ReplyDays(NoticeStage stage) => stage switch
  {
    NoticeStage.First => NoticeSecondDays,
    NoticeStage.Second => NoticeFinalDays - NoticeSecondDays,
    NoticeStage.Final => NoticeShowCauseDays - NoticeFinalDays,
    _ => 10
  };
}
