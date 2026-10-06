/// GP Fund interest for a fiscal year by the monthly product method: the rate / 12 on the balance at the end of each
/// month of the year (months with no balance earn nothing).
public static class GpFundInterest
{
  /// `ledger` = the account's rows up to the year end, without the interest credited for this year.
  public static decimal ForYear(IReadOnlyCollection<(DateOnly Date, decimal Amount)> ledger, DateOnly yearStart, DateOnly yearEnd, decimal ratePercent)
  {
    ArgumentNullException.ThrowIfNull(ledger);
    Guard.DateOrder(yearStart, yearEnd, "Year start", "Year end");

    var total = 0m;
    for (var month = new DateOnly(yearStart.Year, yearStart.Month, 1); month <= yearEnd; month = month.AddMonths(1))
    {
      var monthEnd = month.AddMonths(1).AddDays(-1);
      if (monthEnd > yearEnd)
        monthEnd = yearEnd;

      var balance = ledger.Where(t => t.Date <= monthEnd).Sum(t => t.Amount);
      if (balance > 0)
        total += balance * ratePercent / 100m / 12m;
    }

    return decimal.Round(total, 2, MidpointRounding.AwayFromZero);
  }
}
