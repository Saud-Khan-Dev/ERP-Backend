public sealed record GetDocumentsQueryResult(IReadOnlyList<DocumentDto> Documents);

/// All documents of a property ("all documents of PROP-00125" — schema guide) or of an owner,
/// optionally narrowed to one sub-record or document type. Latest versions only unless asked.
public sealed record GetDocumentsQuery(
  Guid? PropertyId = null,
  Guid? OwnerId = null,
  DocumentEntityType? EntityType = null,
  Guid? EntityId = null,
  Guid? DocumentTypeId = null,
  bool IncludeSuperseded = false,
  bool IncludeInactive = false) : IQuery<Result<GetDocumentsQueryResult>>;
