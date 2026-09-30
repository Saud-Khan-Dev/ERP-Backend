using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// Shared column mappings so every configuration stores ids, codes and enums the same way.
public static class ConversionExtensions
{
  public static PropertyBuilder<MasterId> HasMasterId(this PropertyBuilder<MasterId> builder) =>
      builder.HasConversion(id => id.Value, value => MasterId.Of(value));

  public static PropertyBuilder<MasterId?> HasOptionalMasterId(this PropertyBuilder<MasterId?> builder) =>
      builder.HasConversion(id => id!.Value, value => MasterId.Of(value));

  public static PropertyBuilder<BusinessCode> HasBusinessCode(this PropertyBuilder<BusinessCode> builder, int maxLength) =>
      builder.HasConversion(code => code.Value, value => BusinessCode.Of(value)).HasMaxLength(maxLength);

  /// System states are stored as UPPER_SNAKE text, matching the values in the schema ('ACTIVE',
  /// 'BUILDING_PLAN' ...), while the API keeps the C# names.
  public static PropertyBuilder<TEnum> HasUpperSnakeEnum<TEnum>(this PropertyBuilder<TEnum> builder, int maxLength = 30)
      where TEnum : struct, Enum =>
      builder.HasConversion(value => EnumText.ToUpperSnake(value.ToString()), text => EnumText.Parse<TEnum>(text))
        .HasMaxLength(maxLength);

  /// Same, for an optional value (null stays NULL).
  public static PropertyBuilder<TEnum?> HasNullableUpperSnakeEnum<TEnum>(this PropertyBuilder<TEnum?> builder, int maxLength = 30)
      where TEnum : struct, Enum =>
      builder.HasConversion(
          value => value.HasValue ? EnumText.ToUpperSnake(value.Value.ToString()) : null,
          text => text == null ? null : EnumText.Parse<TEnum>(text))
        .HasMaxLength(maxLength);
}

public static class LowerSnakeEnumExtensions
{
  /// For columns that store a table name (property_appeal.order_source_table = 'property_allotment').
  public static PropertyBuilder<TEnum?> HasLowerSnakeEnum<TEnum>(this PropertyBuilder<TEnum?> builder, int maxLength)
      where TEnum : struct, Enum =>
      builder.HasConversion(
          value => value.HasValue ? EnumText.ToUpperSnake(value.Value.ToString()).ToLowerInvariant() : null,
          text => text == null ? null : EnumText.Parse<TEnum>(text))
        .HasMaxLength(maxLength);
}

public static class EnumText
{
  public static string ToUpperSnake(string name)
  {
    var text = new StringBuilder(name.Length + 4);

    for (var i = 0; i < name.Length; i++)
    {
      if (i > 0 && char.IsUpper(name[i]))
        text.Append('_');

      text.Append(char.ToUpperInvariant(name[i]));
    }

    return text.ToString();
  }

  public static TEnum Parse<TEnum>(string text) where TEnum : struct, Enum =>
      Enum.Parse<TEnum>(text.Replace("_", string.Empty), ignoreCase: true);
}
