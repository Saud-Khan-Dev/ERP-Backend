/// A separation-of-duty rule HRM enforces and whether it is on (switched in Administration › Configuration only).
public sealed record HrmDutyRuleDto(string Code, string Title, string Description, bool IsOn);

/// The service rules in force, for screens that must mirror them (maker-checker, retirement, increments, PER weights).
/// Rules GDA still has to confirm are settings with the behaviour the system had before; the screens can say which is used.
/// RequireSeparateHrApprover / RequireSeparatePayrollApprover: the duty rules HR_ACTION / PAYROLL_APPROVE are on.
public sealed record HrmSettingsDto(
  bool RequireSeparateHrApprover,
  bool RequireSeparatePayrollApprover,
  int RetirementAge,
  bool RetireOnDayBeforeBirthday,
  int AnnualIncrementMonth,
  int AnnualIncrementDay,
  int IncrementQualifyingMonths,
  int PerGoalWeight,
  int PerKpiWeight,
  int PerCompetencyWeight,
  IReadOnlyList<HrmDutyRuleDto> DutyRules);

public sealed record GetHrmSettingsQuery : IQuery<Result<HrmSettingsDto>>;

public class SettingsHandlers(HrmSettings settings, IDutyRules rules) : IQueryHandler<GetHrmSettingsQuery, Result<HrmSettingsDto>>
{
  public async Task<Result<HrmSettingsDto>> Handle(GetHrmSettingsQuery query, CancellationToken cancellationToken)
  {
    var weights = settings.ScoreWeights;
    var duty = new List<HrmDutyRuleDto>();
    foreach (var rule in DutyRules.All.Where(r => r.Area == "Human Resources"))
      duty.Add(new(rule.Code, rule.Title, rule.Description, await rules.IsOnAsync(rule.Code, cancellationToken)));
    bool On(string code) => duty.FirstOrDefault(d => d.Code == code)?.IsOn ?? true;

    return Result<HrmSettingsDto>.Success(new(On(DutyRules.HrAction), On(DutyRules.PayrollApprove), settings.RetirementAge,
      settings.RetireOnDayBeforeBirthday, settings.AnnualIncrementMonth, settings.AnnualIncrementDay, settings.IncrementQualifyingMonths,
      weights.Goals, weights.Kpis, weights.Competencies, duty));
  }
}
