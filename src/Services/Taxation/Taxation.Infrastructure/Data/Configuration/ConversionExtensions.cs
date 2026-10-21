using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
  private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

  /// The DBML's fixed value lists are stored as UPPER_SNAKE text ('QR_CODE'); the API keeps the C# names.
  public static PropertyBuilder<TEnum> UpperSnake<TEnum>(this PropertyBuilder<TEnum> builder, int maxLength) where TEnum : struct, Enum =>
      builder.HasConversion(value => EnumText.ToUpperSnake(value.ToString()), text => EnumText.Parse<TEnum>(text)).HasMaxLength(maxLength);

  public static PropertyBuilder<TEnum?> UpperSnake<TEnum>(this PropertyBuilder<TEnum?> builder, int maxLength) where TEnum : struct, Enum =>
      builder.HasConversion(
          value => value.HasValue ? EnumText.ToUpperSnake(value.Value.ToString()) : null,
          text => text == null ? null : EnumText.Parse<TEnum>(text))
        .HasMaxLength(maxLength);

  /// PostgreSQL's xmin system column as the row's concurrency token: every UPDATE carries "WHERE xmin = <as read>", so a
  /// row changed by someone else since it was read fails the save (409). xmin exists on every table: no column is added.
  public static void XminVersion<TEntity>(this EntityTypeBuilder<TEntity> builder, System.Linq.Expressions.Expression<Func<TEntity, uint>> property) where TEntity : class =>
      builder.Property(property).HasColumnName("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();

  /// numeric(p,s)
  public static PropertyBuilder<decimal> Numeric(this PropertyBuilder<decimal> builder, int precision, int scale) => builder.HasPrecision(precision, scale);
  public static PropertyBuilder<decimal?> Numeric(this PropertyBuilder<decimal?> builder, int precision, int scale) => builder.HasPrecision(precision, scale);

  public static PropertyBuilder<TText> Text<TText>(this PropertyBuilder<TText> builder) => builder.HasColumnType("text");

  /// A value kept as jsonb, compared by its JSON so changes inside it are saved.
  public static PropertyBuilder<T> Jsonb<T>(this PropertyBuilder<T> builder) =>
      builder.HasColumnType("jsonb").HasConversion(new ValueConverter<T, string>(
          value => JsonSerializer.Serialize(value, Json),
          text => JsonSerializer.Deserialize<T>(text, Json)!),
        new ValueComparer<T>(
          (a, b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json),
          v => JsonSerializer.Serialize(v, Json).GetHashCode(),
          v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, Json), Json)!));
}
