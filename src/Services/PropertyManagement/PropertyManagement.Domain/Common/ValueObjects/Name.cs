public sealed record Name
{
  private const int DefaultLength = 200;

  public string Value { get; }

  private Name(string value) => Value = value;

  public static Name Of(string value, int maxLength = DefaultLength)
  {
    if (string.IsNullOrWhiteSpace(value))
      throw new DomainException("Name is required.");

    value = value.Trim();

    if (value.Length > maxLength)
      throw new DomainException($"Name cannot exceed {maxLength} characters.");

    return new Name(value);
  }

  public override string ToString() => Value;
}
