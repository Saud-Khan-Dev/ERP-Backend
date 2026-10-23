/// Who did something: the signed-in user's id and the name to show for them, kept with the record so history reads
/// right even after the account changes. Never taken from a request body - the API fills it from the token.
public sealed record ActorRef(Guid? Id, string? Name)
{
  public static ActorRef System { get; } = new(null, "System");

  public string? ShortName => string.IsNullOrWhiteSpace(Name) ? null : (Name.Length > 100 ? Name[..100] : Name.Trim());
}
