using System.Text;
using Microsoft.EntityFrameworkCore;

public class AddStructureFieldHandler(IApplicationDbContext context)
  : ICommandHandler<AddStructureFieldCommand, Result<AddStructureFieldCommandResult>>
{
  public async Task<Result<AddStructureFieldCommandResult>> Handle(AddStructureFieldCommand command, CancellationToken cancellationToken)
  {
    var target = command.Scope switch
    {
      AttributeScope.AssetClass => AttributeScopeTarget.Of(command.Scope, command.TargetId, null, null, null),
      AttributeScope.AssetType => AttributeScopeTarget.Of(command.Scope, null, command.TargetId, null, null),
      AttributeScope.Category => AttributeScopeTarget.Of(command.Scope, null, null, command.TargetId, null),
      _ => throw new DomainException("Fields are added to a class, a type or a category.")
    };
    await AttributeAssignmentTargets.EnsureExistsAsync(context, target, cancellationToken);

    // ---- the field: reused from the library, or new ----
    AttributeDefinition definition;
    OptionSet? optionSet = null;

    if (command.ExistingDefinitionId is { } existingId)
    {
      var definitionId = AttributeDefinitionId.Of(existingId);
      definition = await context.AttributeDefinitions.AsNoTracking().FirstOrDefaultAsync(d => d.Id == definitionId, cancellationToken)
        ?? throw new AttributeDefinitionNotFoundException($"Field {existingId} was not found.");

      if (!definition.IsActive)
        return Result<AddStructureFieldCommandResult>.Failure($"The field '{definition.Name.Value}' is switched off. Switch it on first.");
    }
    else
    {
      var field = command.Field!;
      var code = AttributeCode.Of(await UniqueAttributeCodeAsync(field.Code ?? field.Label, cancellationToken));

      if (field.DataType is AttributeDataType.Select or AttributeDataType.MultiSelect)
      {
        var setCode = LookupCode.Of(await UniqueOptionSetCodeAsync(code.Value, cancellationToken));
        optionSet = OptionSet.Create(OptionSetId.Of(Guid.NewGuid()), setCode, Name.Of(field.Label, 150), $"Choices of '{field.Label}'.", isSystem: false);

        var order = 1;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var choice in field.Choices!)
        {
          var value = LookupCode.Of(string.IsNullOrWhiteSpace(choice.Value) ? ToUpperSnake(choice.Label) : choice.Value);
          if (!seen.Add(value.Value))
            return Result<AddStructureFieldCommandResult>.Failure($"The choice '{choice.Label}' is listed twice.");
          optionSet.AddValue(OptionSetValueId.Of(Guid.NewGuid()), null, value, choice.Label.Trim(), null, null, order++);
        }

        await context.OptionSets.AddAsync(optionSet, cancellationToken);
      }

      var v = field.Validation;
      definition = AttributeDefinition.Create(
        id: AttributeDefinitionId.Of(Guid.NewGuid()),
        code: code,
        name: Name.Of(field.Label.Trim(), 150),
        description: field.Description,
        dataType: field.DataType,
        optionSetId: optionSet?.Id,
        referenceEntity: null,
        unit: string.IsNullOrWhiteSpace(field.Unit) ? null : field.Unit.Trim(),
        numericPrecision: null,
        numericScale: field.DataType == AttributeDataType.Decimal ? 2 : null,
        rules: v is null
          ? new AttributeValidationRules()
          : new AttributeValidationRules(v.MinNumber, v.MaxNumber, v.MinLength, v.MaxLength, v.MinDate, v.MaxDate, v.RegexPattern, v.IsUniquePerCategory, v.ValidationMessage),
        isMultiValue: false,
        isPii: false,
        isSystem: false);

      await context.AttributeDefinitions.AddAsync(definition, cancellationToken);
    }

    var duplicate = await context.AttributeAssignments.AnyAsync(a =>
        a.AttributeDefinitionId == definition.Id
        && a.AssetClassId == target.AssetClassId
        && a.AssetTypeId == target.AssetTypeId
        && a.CategoryId == target.CategoryId
        && a.AssetId == null, cancellationToken);
    if (duplicate)
      return Result<AddStructureFieldCommandResult>.Failure($"'{definition.Name.Value}' is already on this form.");

    // ---- the form section ----
    var placement = command.Placement;
    AttributeGroupId? sectionId = null;
    if (!string.IsNullOrWhiteSpace(placement.NewSectionName))
    {
      var name = placement.NewSectionName.Trim();
      var sectionCode = LookupCode.Of(ToUpperSnake(name));
      var existing = await context.AttributeGroups.FirstOrDefaultAsync(g => g.Code == sectionCode, cancellationToken);
      if (existing is not null)
      {
        sectionId = existing.Id;
      }
      else
      {
        var order = await context.AttributeGroups.CountAsync(cancellationToken) + 1;
        var section = AttributeGroup.Create(AttributeGroupId.Of(Guid.NewGuid()), sectionCode, Name.Of(name, 150), null, order, isCollapsible: true);
        await context.AttributeGroups.AddAsync(section, cancellationToken);
        sectionId = section.Id;
      }
    }
    else if (placement.SectionId.HasValue)
    {
      sectionId = AttributeGroupId.Of(placement.SectionId.Value);
      if (!await context.AttributeGroups.AnyAsync(g => g.Id == sectionId, cancellationToken))
        throw new AttributeGroupNotFoundException($"Form section {placement.SectionId} was not found.");
    }

    var displayOrder = placement.DisplayOrder ?? await NextDisplayOrderAsync(target, cancellationToken);
    var assignment = AttributeAssignment.Create(
      AttributeAssignmentId.Of(Guid.NewGuid()),
      definition.Id,
      target,
      sectionId,
      new AttributeAssignmentOptions(
        string.IsNullOrWhiteSpace(placement.LabelOverride) ? null : placement.LabelOverride.Trim(),
        placement.IsRequired,
        false,
        placement.IsSearchable,
        placement.IsFilterable,
        placement.IsVisibleInList,
        placement.InheritToChildren,
        placement.DefaultValue,
        displayOrder,
        null,
        null));

    await context.AttributeAssignments.AddAsync(assignment, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<AddStructureFieldCommandResult>.Success(
      new AddStructureFieldCommandResult(assignment.Id.Value, definition.Id.Value, optionSet?.Id.Value, sectionId?.Value));
  }

  private async Task<int> NextDisplayOrderAsync(AttributeScopeTarget target, CancellationToken cancellationToken)
  {
    var orders = await context.AttributeAssignments
      .Where(a => a.AssetClassId == target.AssetClassId && a.AssetTypeId == target.AssetTypeId && a.CategoryId == target.CategoryId && a.AssetId == null)
      .Select(a => a.DisplayOrder)
      .ToListAsync(cancellationToken);
    return (orders.Where(o => o.HasValue).Select(o => o!.Value).DefaultIfEmpty(0).Max()) + 10;
  }

  private async Task<string> UniqueAttributeCodeAsync(string source, CancellationToken cancellationToken)
  {
    var stem = ToLowerSnake(source);
    if (stem.Length == 0 || !char.IsAsciiLetterLower(stem[0])) stem = "field_" + stem;
    stem = stem.Length > 90 ? stem[..90].TrimEnd('_') : stem;

    var taken = (await context.AttributeDefinitions.Select(d => d.Code).ToListAsync(cancellationToken)).Select(c => c.Value).ToHashSet(StringComparer.Ordinal);
    var candidate = stem;
    for (var i = 2; taken.Contains(candidate); i++) candidate = $"{stem}_{i}";
    return candidate;
  }

  private async Task<string> UniqueOptionSetCodeAsync(string fieldCode, CancellationToken cancellationToken)
  {
    var stem = fieldCode.ToUpperInvariant();
    stem = stem.Length > 40 ? stem[..40].TrimEnd('_') : stem;
    var taken = (await context.OptionSets.Select(o => o.Code).ToListAsync(cancellationToken)).Select(c => c.Value).ToHashSet(StringComparer.Ordinal);
    var candidate = stem;
    for (var i = 2; taken.Contains(candidate); i++) candidate = $"{stem}_{i}";
    return candidate;
  }

  /// "Plot facing (main road)" -> "plot_facing_main_road"
  public static string ToLowerSnake(string text)
  {
    var builder = new StringBuilder();
    foreach (var ch in text.Trim().ToLowerInvariant())
    {
      if (char.IsAsciiLetterOrDigit(ch)) builder.Append(ch);
      else if (builder.Length > 0 && builder[^1] != '_') builder.Append('_');
    }
    return builder.ToString().Trim('_');
  }

  public static string ToUpperSnake(string text)
  {
    var lower = ToLowerSnake(text);
    return lower.Length == 0 ? "VALUE" : lower.ToUpperInvariant();
  }
}
