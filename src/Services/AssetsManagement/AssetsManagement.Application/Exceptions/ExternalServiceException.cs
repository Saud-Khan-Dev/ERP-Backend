/// Another service (HRM) did not answer or refused the signed-in user. Becomes a 503 with the sentence.
public class ExternalServiceException : Exception
{
  public ExternalServiceException(string message, Exception? inner = null) : base(message, inner) { }
}
