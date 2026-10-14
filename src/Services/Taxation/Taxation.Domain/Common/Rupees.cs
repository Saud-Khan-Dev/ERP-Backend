using System.Globalization;

/// Taxation amounts are whole rupees: every computed amount is rounded half away from zero, as the Section's sheets do.
public static class Rupees
{
  public static decimal Round(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero);

  /// "Rs 1,234,567" for explanations and messages.
  public static string Text(decimal value) => "Rs " + Round(value).ToString("#,0", CultureInfo.InvariantCulture);

  /// A plain number for explanations: 12,000 or 2.5.
  public static string Number(decimal value) => value.ToString("#,0.####", CultureInfo.InvariantCulture);
}
