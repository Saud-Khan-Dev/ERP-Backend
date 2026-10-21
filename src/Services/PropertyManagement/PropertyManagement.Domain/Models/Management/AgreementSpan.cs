/// The kinds of agreement that occupy a property for a period.
public enum AgreementSpanKind { Allotment, Lease, Rental, Contract }

/// One agreement in force on a property, as the overlap rule reads it: its kind, code, party, the period it runs (End
/// null = open-ended) and its type (contracts of different types - parking and cleaning - may run side by side).
public sealed record AgreementSpan(AgreementSpanKind Kind, string Code, string PartyName, DateOnly Start, DateOnly? End, MasterId? TypeId = null);

/// One property, one agreement of a kind at a time: a lease, a rental or an allotment cannot overlap another of its kind
/// in force on the same property, nor a contract another contract of the same type. Agreements of different kinds may
/// coexist (a shop rented on a leased plot), so they only warn.
public static class AgreementOverlap
{
  public static bool Overlaps(DateOnly aStart, DateOnly? aEnd, DateOnly bStart, DateOnly? bEnd) =>
      aStart <= (bEnd ?? DateOnly.MaxValue) && bStart <= (aEnd ?? DateOnly.MaxValue);

  /// Refuses an overlap with an agreement of the same kind (a contract: of the same type); returns a warning for each
  /// agreement of another kind that runs at the same time. `others` may include the candidate itself and the agreement
  /// it renews (`except`): both are skipped.
  public static IReadOnlyList<string> Check(AgreementSpan candidate, IEnumerable<AgreementSpan> others, string? except = null)
  {
    ArgumentNullException.ThrowIfNull(candidate);
    ArgumentNullException.ThrowIfNull(others);

    var warnings = new List<string>();

    foreach (var other in others.Where(o => o.Code != candidate.Code && o.Code != except && Overlaps(candidate.Start, candidate.End, o.Start, o.End)))
    {
      if (other.Kind == candidate.Kind)
      {
        // two contracts of different types are independent services (parking and cleaning): neither refused nor said
        if (candidate.Kind == AgreementSpanKind.Contract && other.TypeId != candidate.TypeId)
          continue;

        throw new DomainException(
          $"{other.Code} to {other.PartyName} already runs {Period(other)} on this property; a second {Noun(candidate.Kind)} cannot overlap it. "
          + $"Renew or end {other.Code} first{(other.End is { } end ? $", or start the new {Noun(candidate.Kind)} after {end:yyyy-MM-dd}" : "")}.");
      }

      warnings.Add($"This property is also {Verb(other.Kind)} {other.PartyName} ({other.Code}) {Period(other)}.");
    }

    return warnings;
  }

  private static string Period(AgreementSpan span) =>
      span.End is { } end ? $"from {span.Start:yyyy-MM-dd} to {end:yyyy-MM-dd}" : $"from {span.Start:yyyy-MM-dd} with no end date";

  private static string Noun(AgreementSpanKind kind) => kind switch
  {
    AgreementSpanKind.Allotment => "allotment",
    AgreementSpanKind.Lease => "lease",
    AgreementSpanKind.Rental => "rental",
    _ => "contract of this type"
  };

  private static string Verb(AgreementSpanKind kind) => kind switch
  {
    AgreementSpanKind.Allotment => "allotted to",
    AgreementSpanKind.Lease => "leased to",
    AgreementSpanKind.Rental => "rented to",
    _ => "under contract with"
  };
}

/// Date rules every agreement shares.
public static class AgreementDates
{
  /// A termination is a recorded event: not in the future, not after the agreed end (the agreement expires then) and
  /// not before it started - except for an agreement that has not started yet, which is called off today.
  public static void EnsureEndedOn(string code, DateOnly date, DateOnly start, DateOnly? agreedEnd, DateOnly? today)
  {
    if (today is { } day && date > day)
      throw new DomainException($"The termination date cannot be in the future ({date:yyyy-MM-dd}); today is {day:yyyy-MM-dd}.");

    var notStartedYet = today is { } now && start > now;
    if (date < start && !(notStartedYet && date == today))
      throw new DomainException(notStartedYet
        ? $"{code} has not started yet (it starts on {start:yyyy-MM-dd}); call it off today, or terminate it once it has started."
        : $"{code} cannot end before it started ({start:yyyy-MM-dd}).");

    if (agreedEnd is { } end && date > end)
      throw new DomainException($"{code} ends on {end:yyyy-MM-dd} anyway; mark it expired after that instead of terminating it later.");
  }
}
