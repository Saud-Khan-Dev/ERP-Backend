/// The codes the module itself depends on. They are seeded and cannot be deactivated or re-coded.
public static class HeadCodes
{
  // regular heads, built in
  public const string PropertyTax = "PROPERTY_TAX";
  public const string Rent = "RENT";

  // regular heads, seeded and configured by the admin (rates in the rate set)
  public const string Conservancy = "CONSERVANCY";
  public const string Water = "WATER";

  // other charges: what a notice can add by year, and what a carried balance is broken into
  public const string LeaseMoney = "LEASE_MONEY";
  public const string Security = "SECURITY";
  public const string Surcharge = "SURCHARGE";
  public const string Wht236A = "WHT_236A";
  public const string Misc = "MISC";

  // case-by-case fees
  public const string TransferFee = "TRANSFER_FEE";
  public const string BpaFee = "BPA_FEE";
  public const string BpaViolation = "BPA_VIOLATION";
  public const string NonUtilization = "NON_UTILIZATION";
  public const string WaterConnection = "WATER_CONNECTION";
  public const string MalbaSecurity = "MALBA_SECURITY";
  public const string Enlistment = "ENLISTMENT";
  public const string Registration = "REGISTRATION";
  public const string Licence = "LICENCE";
  public const string DocumentCopies = "DOCUMENT_COPIES";

  /// Heads the code refers to by code; they must exist and stay active.
  public static readonly IReadOnlySet<string> System = new HashSet<string>(StringComparer.Ordinal)
  {
    PropertyTax, Rent, LeaseMoney, Security, Surcharge, Wht236A, Misc
  };
}

/// taxation.tax_head: a charge GDA levies. REGULAR heads make up the annual demand, computed in the admin's order:
/// property tax (the LTF formula) and annual rent (the agreement) are built in; every other regular head is defined by
/// the admin with its calculation (band table, rate per unit, fixed amount or formula), the categories it applies to and
/// when it is charged. Its rates live in the rate set. OTHER heads are charged case by case (fees, charges added on a
/// notice).
public class TaxHead : Aggregate<TaxHeadId>
{
  public string Code { get; private set; } = default!;
  public string Name { get; private set; } = default!;
  public HeadKind HeadKind { get; private set; }
  public bool IsRefundable { get; private set; }
  public CalcMethod? CalcMethod { get; private set; }
  public bool IsBuiltin { get; private set; }
  public AppliesTo AppliesTo { get; private set; }
  public ChargedFrom ChargedFrom { get; private set; }
  public string? FacilityLabel { get; private set; }
  public string? FormulaCom { get; private set; }
  public string? FormulaRes { get; private set; }
  public DateOnly? EffectiveFrom { get; private set; }
  public string? BoardReference { get; private set; }
  public bool AppliesAnnualIncrease { get; private set; }
  public bool IsConditional { get; private set; }
  public bool OverrideAllowed { get; private set; }
  public string? AccountsHeadCode { get; private set; }
  public int SortOrder { get; private set; }
  public bool IsActive { get; private set; }

  private TaxHead() { }

  public bool IsSystem => HeadCodes.System.Contains(Code);

  public bool AppliesToCategory(TaxCategory category) =>
      AppliesTo == AppliesTo.Both || (AppliesTo == AppliesTo.Com && category == TaxCategory.Com) || (AppliesTo == AppliesTo.Res && category == TaxCategory.Res);

  public string? FormulaFor(TaxCategory category) => category == TaxCategory.Com ? FormulaCom : FormulaRes;

  /// A built-in regular head (property tax, annual rent); seeded only.
  public static TaxHead Builtin(TaxHeadId id, string code, string name, CalcMethod calc, bool increase, bool overrideAllowed, int sortOrder, string? formula = null) =>
      new()
      {
        Id = id,
        Code = code,
        Name = name,
        HeadKind = HeadKind.Regular,
        CalcMethod = calc,
        IsBuiltin = true,
        AppliesTo = AppliesTo.Both,
        ChargedFrom = ChargedFrom.Always,
        FormulaCom = formula,
        FormulaRes = formula,
        AppliesAnnualIncrease = increase,
        OverrideAllowed = overrideAllowed,
        SortOrder = sortOrder,
        IsActive = true
      };

  /// A regular head the admin defines (conservancy, water, signboard fee ...).
  public static TaxHead Regular(TaxHeadId id, string code, RegularHeadInput input)
  {
    var head = new TaxHead { Id = id, Code = Guard.Code(code, 30, "Head code"), HeadKind = HeadKind.Regular };
    head.ApplyRegular(input);
    return head;
  }

  /// An other (case-by-case) head: a fee or a charge added on a notice.
  public static TaxHead Other(TaxHeadId id, string code, string name, bool isRefundable, int sortOrder, string? accountsHeadCode = null) =>
      new()
      {
        Id = id,
        Code = Guard.Code(code, 30, "Head code"),
        Name = Guard.RequiredText(name, 100, "Head name"),
        HeadKind = HeadKind.Other,
        IsRefundable = isRefundable,
        AppliesTo = AppliesTo.Both,
        ChargedFrom = ChargedFrom.Always,
        SortOrder = sortOrder,
        AccountsHeadCode = Guard.Text(accountsHeadCode, 30, "Accounts head code"),
        IsActive = true
      };

  /// Changes a regular head. A built-in head keeps its calculation; only its name, flags and order change (and the
  /// property tax formula).
  public void UpdateRegular(RegularHeadInput input)
  {
    if (HeadKind != HeadKind.Regular)
      throw new DomainException($"{Name} is charged case by case; it has no annual calculation.");

    if (IsBuiltin)
    {
      Name = Guard.RequiredText(input.Name, 100, "Head name");
      AppliesAnnualIncrease = input.AppliesAnnualIncrease;
      OverrideAllowed = input.OverrideAllowed;
      SortOrder = input.SortOrder;
      BoardReference = Guard.Text(input.BoardReference, 100, "Board reference");
      EffectiveFrom = input.EffectiveFrom;
      AccountsHeadCode = Guard.Text(input.AccountsHeadCode, 30, "Accounts head code");
      if (CalcMethod == global::CalcMethod.Ltf)
      {
        var formula = TaxFormula.Parse(input.FormulaCom ?? input.FormulaRes ?? FormulaCom).Expression;
        FormulaCom = formula;
        FormulaRes = TaxFormula.Parse(input.FormulaRes ?? formula).Expression;
      }
      SetActive(input.IsActive);
      return;
    }

    ApplyRegular(input);
  }

  public void UpdateOther(string name, bool isRefundable, int sortOrder, string? accountsHeadCode)
  {
    if (HeadKind != HeadKind.Other)
      throw new DomainException($"{Name} is a regular head; change it with its calculation.");
    Name = Guard.RequiredText(name, 100, "Head name");
    IsRefundable = isRefundable;
    SortOrder = sortOrder;
    AccountsHeadCode = Guard.Text(accountsHeadCode, 30, "Accounts head code");
  }

  public void SetActive(bool isActive)
  {
    if (!isActive && IsSystem)
      throw new DomainException($"{Name} is used by the module itself and cannot be deactivated.");
    IsActive = isActive;
  }

  private void ApplyRegular(RegularHeadInput input)
  {
    if (input.CalcMethod is global::CalcMethod.Ltf or global::CalcMethod.Agreement)
      throw new DomainException("Property tax (LTF) and annual rent (agreement) are built in; choose bands, rate, fixed or formula.");

    Name = Guard.RequiredText(input.Name, 100, "Head name");
    CalcMethod = input.CalcMethod;
    AppliesTo = input.AppliesTo;
    ChargedFrom = input.ChargedFrom;
    FacilityLabel = Guard.Text(input.FacilityLabel, 60, "Facility");
    if (ChargedFrom == ChargedFrom.Facility && FacilityLabel is null)
      throw new DomainException("Name the facility the head is charged for (e.g. water connection).");
    if (ChargedFrom != ChargedFrom.Facility)
      FacilityLabel = null;
    IsConditional = ChargedFrom == ChargedFrom.Facility;

    FormulaCom = null;
    FormulaRes = null;
    if (CalcMethod == global::CalcMethod.Formula)
    {
      if (AppliesTo != AppliesTo.Res)
        FormulaCom = TaxFormula.Parse(input.FormulaCom ?? throw new DomainException("Give the commercial formula.")).Expression;
      if (AppliesTo != AppliesTo.Com)
        FormulaRes = TaxFormula.Parse(input.FormulaRes ?? throw new DomainException("Give the residential formula.")).Expression;
    }

    AppliesAnnualIncrease = input.AppliesAnnualIncrease;
    OverrideAllowed = input.OverrideAllowed;
    SortOrder = input.SortOrder;
    BoardReference = Guard.Text(input.BoardReference, 100, "Board reference");
    EffectiveFrom = input.EffectiveFrom;
    AccountsHeadCode = Guard.Text(input.AccountsHeadCode, 30, "Accounts head code");
    SetActive(input.IsActive);
  }
}

public sealed record RegularHeadInput(
  string Name,
  CalcMethod CalcMethod,
  AppliesTo AppliesTo,
  ChargedFrom ChargedFrom,
  string? FacilityLabel,
  string? FormulaCom,
  string? FormulaRes,
  bool AppliesAnnualIncrease,
  bool OverrideAllowed,
  int SortOrder,
  bool IsActive,
  string? BoardReference,
  DateOnly? EffectiveFrom,
  string? AccountsHeadCode);
