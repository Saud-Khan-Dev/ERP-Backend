/// A business rule refused the change. The message is the sentence the person reads (the API returns it as a 400),
/// so it says what is wrong and what to do, in plain words - no codes or field names.
public class DomainException : Exception
{
  public DomainException(string message) : base(message) { }
}
