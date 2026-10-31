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

  public DateOnly? SuperannuationDate(Employee employee) => employee.SuperannuationDate(RetirementAge, RetireOnDayBeforeBirthday);
}
