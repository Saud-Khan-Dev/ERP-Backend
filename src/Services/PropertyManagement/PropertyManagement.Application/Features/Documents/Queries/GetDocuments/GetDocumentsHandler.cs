using Microsoft.EntityFrameworkCore;

public class GetDocumentsHandler(IApplicationDbContext context, MasterLookup masters, DocumentService documents)
  : IQueryHandler<GetDocumentsQuery, Result<GetDocumentsQueryResult>>
{
  public async Task<Result<GetDocumentsQueryResult>> Handle(GetDocumentsQuery query, CancellationToken cancellationToken)
  {
    var rows = context.Documents.AsNoTracking();

    if (query.PropertyId is { } p)
    {
      var propertyId = PropertyId.Of(p);
      if (!await context.Properties.AnyAsync(x => x.Id == propertyId, cancellationToken))
        throw new PropertyNotFoundException($"Property {p} was not found.");

      // an owner's own papers are not part of a property's file
      rows = rows.Where(d => d.PropertyId == propertyId && d.EntityType != DocumentEntityType.Owner);
    }

    if (query.OwnerId is { } ownerId)
    {
      await context.EnsureOwnerVisibleAsync(OwnerId.Of(ownerId), cancellationToken);

      rows = rows.Where(d => d.EntityType == DocumentEntityType.Owner && d.EntityId == ownerId);
    }

    if (query.EntityType is { } entityType)
      rows = rows.Where(d => d.EntityType == entityType);

    if (query.EntityId is { } entityId)
      rows = rows.Where(d => d.EntityId == entityId);

    if (query.DocumentTypeId is { } typeId)
      rows = rows.Where(d => d.DocumentTypeId == MasterId.Of(typeId));

    if (!query.IncludeInactive)
      rows = rows.Where(d => d.IsActive);

    var all = await rows.OrderByDescending(d => d.UploadedAt).ToListAsync(cancellationToken);

    var ids = all.Select(d => d.Id).ToList();
    var superseded = (await context.Documents.AsNoTracking()
        .Where(d => d.SupersedesDocumentId != null && ids.Contains(d.SupersedesDocumentId))
        .Select(d => d.SupersedesDocumentId!)
        .ToListAsync(cancellationToken)).ToHashSet();

    if (!query.IncludeSuperseded)
      all = all.Where(d => !superseded.Contains(d.Id)).ToList();

    // confidential papers and papers on legal records the caller may not see are counted, never silently left out
    var list = all.Where(documents.MaySee).ToList();
    var restricted = all.Count - list.Count;

    var refs = await masters.Refs().Add<DocumentType>(list.Select(d => d.DocumentTypeId)).LoadAsync(cancellationToken);

    return Result<GetDocumentsQueryResult>.Success(new GetDocumentsQueryResult(
      list.Select(d => d.ToDto(refs, !superseded.Contains(d.Id))).ToList(), restricted));
  }
}
