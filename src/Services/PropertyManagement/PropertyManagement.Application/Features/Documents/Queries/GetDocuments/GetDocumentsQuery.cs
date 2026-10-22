/// RestrictedCount: papers matching the filters that the caller may not see (confidential, or filed on an encroachment,
/// case or appeal - they need PROPERTY_LEGAL.VIEW); the screen shows them as "Restricted".
public sealed record GetDocumentsQueryResult(IReadOnlyList<DocumentDto> Documents, int RestrictedCount = 0);

/// All documents of a property ("all documents of PROP-00125" — schema guide; an owner's own papers are not among
/// them) or an owner's own papers, optionally narrowed to one sub-record or document type. Latest versions only
/// unless asked.
public sealed record GetDocumentsQuery(
  Guid? PropertyId = null,
  Guid? OwnerId = null,
  DocumentEntityType? EntityType = null,
  Guid? EntityId = null,
  Guid? DocumentTypeId = null,
  bool IncludeSuperseded = false,
  bool IncludeInactive = false) : IQuery<Result<GetDocumentsQueryResult>>;
