using Microsoft.Extensions.Options;

/// The service rules in force (from configuration), read in one place. Rules GDA still has to confirm default to the
/// behaviour the system had before they became settings.
public class HrmSettings(IOptions<HrmOptions> hrm)
{
  public int RetirementAge => hrm.Value.RetirementAge;

  public bool RetireOnDayBeforeBirthday => hrm.Value.RetireOnDayBeforeBirthday;

  public int IncrementQualifyingMonths => hrm.Value.IncrementQualifyingMonths;

  public int AnnualIncrementMonth => hrm.Value.AnnualIncrementMonth;

  public int AnnualIncrementDay => hrm.Value.AnnualIncrementDay;


  /// The PER score weights; a set that does not total 100 falls back to goals only.
  public ScoreWeights ScoreWeights
  {
    get
    {
      var h = hrm.Value;
      return h.PerGoalWeight + h.PerKpiWeight + h.PerCompetencyWeight == 100 && h.PerGoalWeight >= 0 && h.PerKpiWeight >= 0 && h.PerCompetencyWeight >= 0
        ? new ScoreWeights(h.PerGoalWeight, h.PerKpiWeight, h.PerCompetencyWeight)
        : ScoreWeights.GoalsOnly;
    }
  }

  public DateOnly? SuperannuationDate(Employee employee) => employee.SuperannuationDate(RetirementAge, RetireOnDayBeforeBirthday);
}
