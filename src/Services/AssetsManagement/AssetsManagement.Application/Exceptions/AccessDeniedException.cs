/// The caller holds the endpoint's permission but not the one this particular change needs (e.g. registering an asset
/// WITH its purchase needs ASSET_FINANCE.CREATE as well). Becomes a 403 with the sentence.
public class AccessDeniedException : Exception
{
  public AccessDeniedException(string message) : base(message) { }
}
