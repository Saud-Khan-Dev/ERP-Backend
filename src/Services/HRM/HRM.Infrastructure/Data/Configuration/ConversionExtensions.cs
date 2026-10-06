using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

/// Maps any ITypedId to its Guid (registered for every id type by convention in ApplicationDbContext).
public sealed class TypedIdConverter<TId>() : ValueConverter<TId, Guid>(id => id.Value, value => Create(value))
  where TId : class, ITypedId<TId>
{
  private static TId Create(Guid value) => TId.Of(value);
}

/// The schema's column types, so every configuration writes them the same way.
public static class ConversionExtensions
{
  /// numeric(14,2)
  public static PropertyBuilder<decimal> Money(this PropertyBuilder<decimal> builder) => builder.HasPrecision(14, 2);
  public static PropertyBuilder<decimal?> Money(this PropertyBuilder<decimal?> builder) => builder.HasPrecision(14, 2);

  /// numeric(p,s) for days, hours, rates and scores.
  public static PropertyBuilder<decimal> Numeric(this PropertyBuilder<decimal> builder, int precision, int scale) => builder.HasPrecision(precision, scale);
  public static PropertyBuilder<decimal?> Numeric(this PropertyBuilder<decimal?> builder, int precision, int scale) => builder.HasPrecision(precision, scale);

  /// text (unbounded strings are varchar by convention).
  public static PropertyBuilder<TText> Text<TText>(this PropertyBuilder<TText> builder) => builder.HasColumnType("text");

  public static PropertyBuilder<TText> Jsonb<TText>(this PropertyBuilder<TText> builder) => builder.HasColumnType("jsonb");
}
