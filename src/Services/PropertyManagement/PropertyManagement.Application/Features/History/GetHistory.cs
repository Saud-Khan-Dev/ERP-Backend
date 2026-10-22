using Microsoft.EntityFrameworkCore;

/// The audit trail of one property (its record and every record on it: shares, transfers and their parties, charges,
/// agreements, bids, legal matters with their parties and hearings, papers, custom-field values) or of one owner (their
/// record, contacts, addresses and own papers), newest first. Filter: AuditFilter (before / from / to / action / search /
/// pageSize); the record itself is fixed by the route.
///
/// Lines of legal records (encroachments, cases with their parties and hearings, appeals) are listed only to callers with
/// PROPERTY_LEGAL.VIEW, and lines of papers they may not see (confidential, or filed on a legal record) are left out too;
/// RestrictedCount says how many lines were held back.
public sealed record GetHistoryQuery(Guid? PropertyId, Guid? OwnerId, AuditFilter Filter) : IQuery<Result<HistoryResult>>;

public sealed record HistoryResult(AuditPageDto Audit, long RestrictedCount);

public class GetHistoryHandler(IApplicationDbContext context, DocumentService documents)
  : IQueryHandler<GetHistoryQuery, Result<HistoryResult>>
{
  /// The record kinds of legal matters, as the audit profile names them (PropertyAudit).
  public static readonly string[] LegalKinds = [PropertyAudit.Encroachment, PropertyAudit.CourtCase, PropertyAudit.CaseParty, PropertyAudit.Hearing, PropertyAudit.Appeal];

  public async Task<Result<HistoryResult>> Handle(GetHistoryQuery query, CancellationToken cancellationToken)
  {
    Guid parent;
    if (query.PropertyId is { } propertyId)
    {
      // 404 for a property that is not on file or outside the caller's towns
      await context.EnsurePropertyExistsAsync(propertyId, cancellationToken);
      parent = propertyId;
    }
    else
    {
      var ownerId = query.OwnerId ?? throw new DomainException("Say whose history to read.");
      await context.EnsureOwnerVisibleAsync(OwnerId.Of(ownerId), cancellationToken);
      parent = ownerId;
    }

    var all = context.AuditTrail.AsNoTracking().Where(x => x.ParentId == parent || x.RecordId == parent);
    var lines = all;
    if (!documents.CanSeeConfidential())
    {
      // papers the caller may not see, on this property or this owner
      var papers = context.Documents.AsNoTracking();
      if (query.PropertyId is not null)
      {
        var propertyKey = PropertyId.Of(parent);
        papers = papers.Where(d => d.PropertyId == propertyKey);
      }
      else
        papers = papers.Where(d => d.EntityType == DocumentEntityType.Owner && d.EntityId == parent);
      var legalRecords = DocumentService.LegalRecordKinds.ToList();
      var hidden = await papers
        .Where(d => d.IsConfidential || legalRecords.Contains(d.EntityType))
        .Select(d => d.Id)
        .ToListAsync(cancellationToken);
      var hiddenIds = hidden.Select(d => (Guid?)d.Value).ToList();
      lines = lines.Where(x => !LegalKinds.Contains(x.RecordKind) && !hiddenIds.Contains(x.RecordId));
    }

    var page = await AuditQueries.ListAsync(lines, query.Filter with { ParentId = null, RecordId = null }, cancellationToken);
    var restricted = ReferenceEquals(lines, all) ? 0 : await all.LongCountAsync(cancellationToken) - await lines.LongCountAsync(cancellationToken);
    return Result<HistoryResult>.Success(new HistoryResult(page, restricted));
  }
}
