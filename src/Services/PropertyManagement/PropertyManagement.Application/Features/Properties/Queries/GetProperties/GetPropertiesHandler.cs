using Microsoft.EntityFrameworkCore;

public class GetPropertiesHandler(IApplicationDbContext context, MasterLookup masters)
  : IQueryHandler<GetPropertiesQuery, Result<GetPropertiesQueryResult>>
{
  public async Task<Result<GetPropertiesQueryResult>> Handle(GetPropertiesQuery query, CancellationToken cancellationToken)
  {
    var properties = context.Properties.AsNoTracking();

    if (!query.IncludeInactive)
      properties = properties.Where(p => p.IsActive);

    if (query.TownId is { } townId)
      properties = properties.Where(p => p.TownId == MasterId.Of(townId));

    if (query.PropertyTypeId is { } typeId)
      properties = properties.Where(p => p.PropertyTypeId == MasterId.Of(typeId));

    if (query.PropertyStatusId is { } statusId)
      properties = properties.Where(p => p.PropertyStatusId == MasterId.Of(statusId));

    if (query.PropertyClassificationId is { } classificationId)
      properties = properties.Where(p => p.PropertyClassificationId == MasterId.Of(classificationId));

    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var term = query.Search.Trim();

      // the code is a value-converted identifier, so it matches exactly; the name matches partially
      BusinessCode? code = null;
      try { code = BusinessCode.Of(term); } catch (DomainException) { }

      var pattern = $"%{term.ToLowerInvariant()}%";
      properties = properties.Where(p =>
          (code != null && p.PropertyCode == code)
          || EF.Functions.Like(p.PropertyName.Value.ToLower(), pattern)
          || (p.KhasraSurveyNo != null && EF.Functions.Like(p.KhasraSurveyNo.ToLower(), pattern)));
    }

    var total = await properties.LongCountAsync(cancellationToken);

    var page = await properties
        .OrderBy(p => p.PropertyCode)
        .Skip(query.Pagination.Pageindex * query.Pagination.PageSize)
        .Take(query.Pagination.PageSize)
        .ToListAsync(cancellationToken);

    var refs = await masters.Refs()
        .Add<Town>(page.Select(p => p.TownId))
        .Add<PropertyType>(page.Select(p => p.PropertyTypeId))
        .Add<PropertyStatus>(page.Select(p => p.PropertyStatusId))
        .Add<PropertyClassification>(page.Select(p => p.PropertyClassificationId))
        .LoadAsync(cancellationToken);

    var data = page.Select(p => p.ToListItemDto(refs)).ToList();

    return Result<GetPropertiesQueryResult>.Success(new GetPropertiesQueryResult(
      new PaginatedResult<PropertyListItemDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }
}
