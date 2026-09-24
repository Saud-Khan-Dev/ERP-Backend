/// An already-hashed password. Wrapping it makes it impossible to assign a plaintext string to the
/// property by accident, and ToString() never reveals the hash.
public sealed record PasswordHash
{
  public string Value { get; }

  private PasswordHash(string value) => Value = value;

  public static PasswordHash Of(string hashedValue)
  {
    if (string.IsNullOrWhiteSpace(hashedValue))
      throw new DomainException("Password hash is required.");

    return new PasswordHash(hashedValue);
  }

  public override string ToString() => "********";
}
