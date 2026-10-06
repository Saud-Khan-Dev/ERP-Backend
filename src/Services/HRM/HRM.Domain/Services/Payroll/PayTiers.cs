using System.Text.Json;

/// Bands for salary rules with calculation method "tiered", kept in the rule's formula_expression as JSON. The band the
/// calculation base falls in decides the amount: a fixed amount, or a percentage of the base.
///   [{"upTo": 50000, "amount": 1500}, {"upTo": 100000, "rate": 3}, {"upTo": null, "amount": 4000}]
/// Bands are checked in order; the first whose upTo is at or above the base (or null) wins. Not progressive: only the
/// matching band applies.
public sealed class PayTiers
{
  public sealed record Band(decimal? UpTo, decimal? Amount, decimal? Rate);

  public IReadOnlyList<Band> Bands { get; }

  private PayTiers(IReadOnlyList<Band> bands) => Bands = bands;

  public static PayTiers Parse(string? json)
  {
    if (string.IsNullOrWhiteSpace(json))
      throw new DomainException("A tiered rule needs its bands, e.g. [{\"upTo\": 50000, \"amount\": 1500}, {\"upTo\": null, \"rate\": 3}].");

    List<Band>? bands;
    try
    {
      bands = JsonSerializer.Deserialize<List<Band>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
    catch (JsonException)
    {
      throw new DomainException("The bands of a tiered rule must be a JSON list such as [{\"upTo\": 50000, \"amount\": 1500}, {\"upTo\": null, \"rate\": 3}].");
    }

    if (bands is null || bands.Count == 0)
      throw new DomainException("A tiered rule needs at least one band.");

    decimal? previous = null;
    for (var i = 0; i < bands.Count; i++)
    {
      var band = bands[i];
      if ((band.Amount is null) == (band.Rate is null))
        throw new DomainException($"Band {i + 1} needs either an amount or a rate, not both.");
      if (band.Amount is < 0 || band.Rate is < 0 or > 1000)
        throw new DomainException($"Band {i + 1}: the amount cannot be negative and the rate must be 0-1000%.");
      if (band.UpTo is null && i != bands.Count - 1)
        throw new DomainException("Only the last band can be open-ended (upTo null).");
      if (band.UpTo is { } upTo && previous is { } before && upTo <= before)
        throw new DomainException("Band limits must increase from one band to the next.");
      previous = band.UpTo ?? previous;
    }

    return new PayTiers(bands);
  }

  /// The amount for a base, or 0 when the base is above the last (closed) band.
  public decimal Evaluate(decimal baseAmount)
  {
    var band = Bands.FirstOrDefault(b => b.UpTo is null || baseAmount <= b.UpTo.Value);
    if (band is null)
      return 0;

    return band.Amount ?? baseAmount * band.Rate!.Value / 100m;
  }
}
