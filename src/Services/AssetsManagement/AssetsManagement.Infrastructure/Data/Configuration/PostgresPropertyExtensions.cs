using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// Shared mappings for the PostgreSQL-specific column types used by the asset module
/// (jsonb / ltree) so every configuration stores them the same way.
public static class PostgresPropertyExtensions
{
  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);

  /// Nullable JSON fragment (default_value, depends_on_value, old_value, details ...) stored as jsonb.
  public static PropertyBuilder<JsonElement?> HasJsonb(this PropertyBuilder<JsonElement?> builder)
  {
    return builder
        .HasConversion(
            element => element.HasValue ? element.Value.GetRawText() : null,
            json => json == null ? null : JsonSerializer.Deserialize<JsonElement>(json, JsonOptions),
            new ValueComparer<JsonElement?>(
                (a, b) => RawText(a) == RawText(b),
                v => RawText(v) == null ? 0 : RawText(v)!.GetHashCode(),
                v => v.HasValue ? v.Value.Clone() : null))
        .HasColumnType("jsonb");
  }

  /// Dynamic attribute bag ({"ram_gb":16,"features":["WIFI"]}) stored as jsonb.
  public static PropertyBuilder<Dictionary<string, JsonElement>> HasJsonbDictionary(this PropertyBuilder<Dictionary<string, JsonElement>> builder)
  {
    return builder
        .HasConversion(
            bag => JsonSerializer.Serialize(bag, JsonOptions),
            json => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions) ?? new Dictionary<string, JsonElement>(),
            new ValueComparer<Dictionary<string, JsonElement>>(
                (a, b) => JsonSerializer.Serialize(a, JsonOptions) == JsonSerializer.Serialize(b, JsonOptions),
                v => JsonSerializer.Serialize(v, JsonOptions).GetHashCode(),
                v => v.ToDictionary(p => p.Key, p => p.Value.Clone(), StringComparer.Ordinal)))
        .HasColumnType("jsonb")
        .HasDefaultValueSql("'{}'::jsonb");
  }

  /// Materialized path (physical.it_equipment.laptop) stored as a native ltree so GIST subtree queries work.
  public static PropertyBuilder<string> HasLtree(this PropertyBuilder<string> builder)
  {
    return builder
        .HasConversion(path => new LTree(path), ltree => ltree.ToString())
        .HasColumnType("ltree");
  }

  public static PropertyBuilder<string?> HasNullableLtree(this PropertyBuilder<string?> builder)
  {
    return builder
        .HasConversion(path => path == null ? default(LTree?) : new LTree(path), ltree => ltree.HasValue ? ltree.Value.ToString() : null)
        .HasColumnType("ltree");
  }

  /// Enums are stored as their names (matches the rest of the service).
  public static PropertyBuilder<TEnum> HasEnumString<TEnum>(this PropertyBuilder<TEnum> builder, int maxLength = 30) where TEnum : struct, Enum
  {
    return builder.HasConversion<string>().HasMaxLength(maxLength);
  }

  private static string? RawText(JsonElement? element) => element.HasValue ? element.Value.GetRawText() : null;
}
