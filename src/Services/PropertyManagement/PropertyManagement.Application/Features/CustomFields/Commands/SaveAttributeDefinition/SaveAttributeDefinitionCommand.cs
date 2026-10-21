using FluentValidation;

/// A custom field on the property form. Code: optional - the service makes one from the label (it is immutable).
/// AppliesToPropertyTypeIds / AppliesToClassificationIds: the properties it is shown on (empty = all).
public sealed record AttributeDefinitionInput(
  Guid AttributeGroupId,
  string? Code,
  string Label,
  AttributeDataType DataType,
  bool IsRequired = false,
  string? OptionsCsv = null,
  string? DefaultValue = null,
  int DisplayOrder = 0,
  IReadOnlyList<Guid>? AppliesToPropertyTypeIds = null,
  IReadOnlyList<Guid>? AppliesToClassificationIds = null);

public sealed record SaveAttributeDefinitionCommandResult(Guid Id, string Code);

/// Id null creates the field; otherwise updates it.
public sealed record SaveAttributeDefinitionCommand(Guid? Id, AttributeDefinitionInput Definition) : ICommand<Result<SaveAttributeDefinitionCommandResult>>;

public class SaveAttributeDefinitionCommandValidator : AbstractValidator<SaveAttributeDefinitionCommand>
{
  public SaveAttributeDefinitionCommandValidator()
  {
    RuleFor(x => x.Definition).NotNull();
    RuleFor(x => x.Definition.AttributeGroupId).NotEmpty().WithMessage("Choose the group the field is shown in.");
    RuleFor(x => x.Definition.Code).MaximumLength(MasterCode.MaxLength);
    RuleFor(x => x.Definition.Label).NotEmpty().WithMessage("Give the field a name.").MaximumLength(100);
    RuleFor(x => x.Definition.DataType).IsInEnum();
    RuleFor(x => x.Definition.OptionsCsv).MaximumLength(2000);
    RuleFor(x => x.Definition.DefaultValue).MaximumLength(AttributeDefinition.MaxTextLength);
  }
}

public static class AttributeCodes
{
  /// A stable key made from the label: letters and digits in capitals joined by "_" ("Plot facing (road)" -> PLOT_FACING_ROAD),
  /// at most 30 characters, made unique with _2, _3 ... A label of other scripts only gets FIELD.
  public static string FromLabel(string label, ISet<string> taken)
  {
    var words = System.Text.RegularExpressions.Regex.Split(label.ToUpperInvariant(), "[^A-Z0-9]+").Where(w => w.Length > 0).ToList();
    var stem = words.Count == 0 ? "FIELD" : string.Join('_', words);
    if (char.IsDigit(stem[0])) stem = "F_" + stem;
    stem = stem.Length > MasterCode.MaxLength ? stem[..MasterCode.MaxLength].TrimEnd('_') : stem;

    var code = stem;
    for (var n = 2; taken.Contains(code); n++)
    {
      var suffix = $"_{n}";
      code = (stem.Length + suffix.Length > MasterCode.MaxLength ? stem[..(MasterCode.MaxLength - suffix.Length)].TrimEnd('_') : stem) + suffix;
    }

    return code;
  }
}
