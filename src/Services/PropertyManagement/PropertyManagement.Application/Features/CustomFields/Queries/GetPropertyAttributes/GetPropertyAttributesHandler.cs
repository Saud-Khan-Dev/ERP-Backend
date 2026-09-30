using Microsoft.EntityFrameworkCore;

public class GetPropertyAttributesHandler(IApplicationDbContext context, MasterLookup masters)
  : IQueryHandler<GetPropertyAttributesQuery, Result<GetPropertyAttributesQueryResult>>
{
  public async Task<Result<GetPropertyAttributesQueryResult>> Handle(GetPropertyAttributesQuery query, CancellationToken cancellationToken)
  {
    var propertyId = PropertyId.Of(query.PropertyId);

    if (!await context.Properties.AnyAsync(p => p.Id == propertyId, cancellationToken))
      throw new PropertyNotFoundException($"Property {query.PropertyId} was not found.");

    var definitions = context.AttributeDefinitions.AsNoTracking().Where(d => d.IsActive);
    if (query.AttributeGroupId is { } groupId)
      definitions = definitions.Where(d => d.AttributeGroupId == MasterId.Of(groupId));

    var fields = await definitions.OrderBy(d => d.DisplayOrder).ThenBy(d => d.Code).ToListAsync(cancellationToken);
    var values = await context.PropertyAttributeValues.AsNoTracking()
        .Where(v => v.PropertyId == propertyId)
        .ToListAsync(cancellationToken);

    var refs = await masters.Refs().Add<AttributeGroup>(fields.Select(d => d.AttributeGroupId)).LoadAsync(cancellationToken);

    var form = fields.Select(d =>
    {
      var value = values.FirstOrDefault(v => v.AttributeDefinitionId == d.Id);
      return new PropertyAttributeValueDto(
        value?.Id.Value, d.Id.Value, refs[d.AttributeGroupId], d.Code.Value, d.Label.Value, d.DataType,
        d.IsRequired, d.Options, d.DisplayOrder, value is null ? d.DefaultValue : value.Value);
    }).ToList();

    return Result<GetPropertyAttributesQueryResult>.Success(new GetPropertyAttributesQueryResult(form));
  }
}
