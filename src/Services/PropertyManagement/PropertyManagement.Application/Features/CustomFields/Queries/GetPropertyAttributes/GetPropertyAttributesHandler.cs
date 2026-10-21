using Microsoft.EntityFrameworkCore;

/// The fields that apply to the property (its type and classification), switched on in a switched-on group, with
/// their values or defaults; then, read-only (IsHidden), the values the property holds for fields that are switched
/// off, sit in a switched-off group or no longer apply - they stay readable, never editable.
public class GetPropertyAttributesHandler(IApplicationDbContext context, MasterLookup masters)
  : IQueryHandler<GetPropertyAttributesQuery, Result<GetPropertyAttributesQueryResult>>
{
  public async Task<Result<GetPropertyAttributesQueryResult>> Handle(GetPropertyAttributesQuery query, CancellationToken cancellationToken)
  {
    var propertyId = PropertyId.Of(query.PropertyId);
    var property = await context.Properties.AsNoTracking().FirstOrDefaultAsync(p => p.Id == propertyId, cancellationToken)
      ?? throw new PropertyNotFoundException($"Property {query.PropertyId} was not found.");

    var definitions = context.AttributeDefinitions.AsNoTracking();
    if (query.AttributeGroupId is { } groupId)
      definitions = definitions.Where(d => d.AttributeGroupId == MasterId.Of(groupId));

    var all = await definitions.OrderBy(d => d.DisplayOrder).ThenBy(d => d.Code).ToListAsync(cancellationToken);
    var values = await context.PropertyAttributeValues.AsNoTracking()
        .Where(v => v.PropertyId == propertyId)
        .ToListAsync(cancellationToken);
    var activeGroups = (await context.Set<AttributeGroup>().AsNoTracking().Where(g => g.IsActive).Select(g => g.Id).ToListAsync(cancellationToken)).ToHashSet();

    bool Editable(AttributeDefinition d) =>
        d.IsActive && activeGroups.Contains(d.AttributeGroupId) && d.AppliesTo(property.PropertyTypeId.Value, property.PropertyClassificationId.Value);

    var refs = await masters.Refs().Add<AttributeGroup>(all.Select(d => d.AttributeGroupId)).LoadAsync(cancellationToken);

    var form = all
      .Select(d => (Definition: d, Value: values.FirstOrDefault(v => v.AttributeDefinitionId == d.Id)))
      .Where(x => Editable(x.Definition) || x.Value?.Value is not null)
      .Select(x => new PropertyAttributeValueDto(
        x.Value?.Id.Value, x.Definition.Id.Value, refs[x.Definition.AttributeGroupId], x.Definition.Code.Value, x.Definition.Label.Value,
        x.Definition.DataType, x.Definition.IsRequired, x.Definition.Options, x.Definition.DisplayOrder,
        x.Value is null ? x.Definition.DefaultValue : x.Value.Value, !Editable(x.Definition)))
      .OrderBy(x => x.IsHidden)
      .ToList();

    return Result<GetPropertyAttributesQueryResult>.Success(new GetPropertyAttributesQueryResult(form));
  }
}
