public class DomainException : Exception
{
  public DomainException(string message) : base($"Domain Exception \"{message} \" throws from Domain layer") => Reason = message;

  /// The rule's own sentence, without the wrapping (an act that re-words a refusal - "Step 3: ..." - uses it).
  public string Reason { get; }
}
