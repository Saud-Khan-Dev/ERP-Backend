using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

/// The separation-of-duty rules: two steps of one piece of work that must be done by two different people (maker and
/// checker). Every rule is ON unless a Super Administrator switched it off in Administration › Configuration, with a
/// reason the activity trail keeps. A service asks <see cref="IDutyRules"/> before the second step and refuses it when
/// the rule is on and the same person did the first.
///
/// The permission conflicts (<see cref="PermissionCatalog.Conflicts"/>) warn when one role carries both halves; these
/// rules block the act itself, whoever holds what.
public static class DutyRules
{
  public sealed record DutyRule(string Code, string Area, string Title, string Description);

  /// Nobody decides on their own employee record: their pay, loans, bank account, payslip, leave, requests, HR actions
  /// about them, their documents and personal details.
  public const string OwnRecord = "OWN_RECORD";
  public const string HrAction = "HR_ACTION";
  public const string PayrollApprove = "PAYROLL_APPROVE";
  public const string PayrollPay = "PAYROLL_PAY";
  public const string LoanSanction = "LOAN_SANCTION";
  public const string LeaveApprove = "LEAVE_APPROVE";
  public const string PropertyTransfer = "PROPERTY_TRANSFER";
  public const string TaxDemandRun = "TAX_DEMAND_RUN";
  public const string TaxRateSet = "TAX_RATE_SET";
  public const string TaxPaymentReverse = "TAX_PAYMENT_REVERSE";
  public const string TaxFeeWaive = "TAX_FEE_WAIVE";
  public const string AssetDisposal = "ASSET_DISPOSAL";
  public const string DepreciationReverse = "DEPRECIATION_REVERSE";

  public static IReadOnlyList<DutyRule> All { get; } =
  [
    new(OwnRecord, "Human Resources", "Nobody decides on their own record",
      "An officer cannot change their own pay, salary bank account, payslip or personal details, sanction their own loan, approve their own leave, request or HR action, or verify their own documents."),
    new(HrAction, "Human Resources", "HR actions pass through two people",
      "An appointment, transfer, promotion or other HR action is approved by someone other than the person who drafted or submitted it."),
    new(PayrollApprove, "Human Resources", "Payroll is approved by someone else",
      "A payroll run is approved by someone other than the person who prepared, reviewed or adjusted it."),
    new(PayrollPay, "Human Resources", "Payroll is paid by someone else",
      "A payroll run is finalized and paid out by someone other than the person who approved it."),
    new(LoanSanction, "Human Resources", "Loans pass through two people",
      "A loan is sanctioned by someone other than the person who entered the application."),
    new(LeaveApprove, "Human Resources", "Leave entered by a clerk is approved by someone else",
      "Leave entered on someone's behalf is approved by someone other than the person who entered it."),
    new(PropertyTransfer, "Property", "Transfers pass through two people",
      "A transfer of ownership - including one recorded after it happened - is approved by someone other than the person who started or recorded it."),
    new(TaxDemandRun, "Taxation", "Demand runs pass through two people",
      "The annual demand is approved by someone other than the person who computed it."),
    new(TaxRateSet, "Taxation", "The tariff passes through two people",
      "A rate set is approved by someone other than the person who drafted it."),
    new(TaxPaymentReverse, "Taxation", "A cashier does not reverse their own receipts",
      "A payment is reversed by someone other than the person who posted it."),
    new(TaxFeeWaive, "Taxation", "Fees are waived by someone else",
      "A fee is waived or exempted by someone other than the person who charged it."),
    new(AssetDisposal, "Assets", "Disposals pass through two people",
      "An asset disposal is approved by someone other than the person who proposed it."),
    new(DepreciationReverse, "Assets", "Depreciation is reversed by someone else",
      "A posted depreciation entry is reversed by someone other than the person who posted it."),
  ];

  public static DutyRule? Find(string code) => All.FirstOrDefault(r => r.Code == code);
}

/// Reads whether a duty rule is on. Identity keeps the switches; the other services ask it.
public interface IDutyRules
{
  Task<bool> IsOnAsync(string code, CancellationToken cancellationToken = default);
}

public static class DutyRuleExtensions
{
  /// The refusal in words when `code` is on and the person acting now (`actorId`) is the one who did the first step
  /// (`makerId`); null when the act may go ahead. `act` completes "You cannot ..." ("approve a transfer you started").
  public static async Task<string?> SeparationRefusalAsync(this IDutyRules rules, string code, Guid? makerId, Guid? actorId, string act,
      CancellationToken cancellationToken = default)
  {
    if (makerId is null || actorId is null || makerId != actorId)
      return null;
    if (!await rules.IsOnAsync(code, cancellationToken))
      return null;
    var rule = DutyRules.Find(code);
    return $"You cannot {act}: {rule?.Title.ToLowerInvariant() ?? "this needs a second person"} (Administration › Configuration › Separation of duties).";
  }
}

/// `GET /duty-rules/state` on Identity: every rule's code and whether it is on. Open to every caller (a background job
/// has no token) - it says nothing but which checks are on.
public sealed record DutyRuleStateDto(string Code, bool IsOn);

public sealed record DutyRuleStatesResponse(IReadOnlyList<DutyRuleStateDto> Rules);

/// The business services' reader: one call a minute at most; when Identity does not answer, the last answer, and before
/// any answer every rule is ON (a check that cannot be read is kept, never dropped).
public sealed class HttpDutyRules(IHttpClientFactory httpClients, IMemoryCache cache, ILogger<HttpDutyRules> logger) : IDutyRules
{
  private const string Key = "duty-rules";
  private const string LastKnownKey = "duty-rules:last";

  public async Task<bool> IsOnAsync(string code, CancellationToken cancellationToken = default)
  {
    var states = await StatesAsync(cancellationToken);
    return !states.TryGetValue(code, out var on) || on;
  }

  private async Task<IReadOnlyDictionary<string, bool>> StatesAsync(CancellationToken cancellationToken)
  {
    if (cache.TryGetValue(Key, out IReadOnlyDictionary<string, bool>? fresh) && fresh is not null)
      return fresh;
    try
    {
      var response = await httpClients.CreateClient(HttpTokenStateChecker.ClientName)
        .GetFromJsonAsync<DutyRuleStatesResponse>("duty-rules/state", cancellationToken);
      var states = (response?.Rules ?? []).ToDictionary(r => r.Code, r => r.IsOn, StringComparer.Ordinal);
      cache.Set(Key, (IReadOnlyDictionary<string, bool>)states, TimeSpan.FromMinutes(1));
      cache.Set(LastKnownKey, (IReadOnlyDictionary<string, bool>)states);
      return states;
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
    {
      logger.LogWarning("Identity did not answer the duty rules ({Message}); keeping every rule on that is not known off.", ex.Message);
      return cache.TryGetValue(LastKnownKey, out IReadOnlyDictionary<string, bool>? last) && last is not null
        ? last
        : new Dictionary<string, bool>();
    }
  }
}
