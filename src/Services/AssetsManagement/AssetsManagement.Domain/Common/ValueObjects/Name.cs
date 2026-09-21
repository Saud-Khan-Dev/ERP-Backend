public sealed record Name
{
  private const int DefaultLength = 100;
  public string Value { get; }
  private Name(string value) => Value = value;

  /// maxLength lets wider ERD columns (asset.name 200, asset_category.name 150) reuse the same value object.
  public static Name Of(string value, int maxLength = DefaultLength)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(value);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Length, maxLength);

    return new Name(value.Trim());
  }
}
