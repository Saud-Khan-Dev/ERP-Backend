using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

/// Each kind is filtered in SQL on its own table, then the four are merged, ordered and paged in memory; property
/// names, parties and master values are read for the page only.
/// Open / closed follows OpenMatters. Dates, per kind:
///   encroachment — opened: detection date; next: none (the record holds no notice deadline)
///   case         — opened: filing date; next: next hearing date
///   appeal       — opened: appeal date; next: decision due date (appeal date + 120 days, Act s.32)
///   violation    — opened: violation date; next: the deadline given in the notice
public class GetLegalMattersHandler(IApplicationDbContext context, MasterLookup masters, PropertyReadService read)
  : IQueryHandler<GetLegalMattersQuery, Result<GetLegalMattersQueryResult>>
{
  private sealed record Row(
    LegalMatterKind Kind, Guid Id, string Code, string? Title, PropertyId PropertyId, string? PartyName, OwnerId? PartyOwnerId,
    string? Court, MasterId? StatusId, StatusRef? FixedStatus, DateOnly? Opened, DateOnly? Next, decimal? AreaSqFt,
    decimal? FineAmount, FineStatus? FineStatus, bool IsOpen);

  private static readonly Expression<Func<PropertyEncroachment, DateOnly?>> EncroachmentOpened = x => x.DetectionDate;
  private static readonly Expression<Func<PropertyLitigation, DateOnly?>> CaseOpened = x => x.FilingDate;
  private static readonly Expression<Func<PropertyLitigation, DateOnly?>> CaseNext = x => x.NextHearingDate;
  private static readonly Expression<Func<PropertyAppeal, DateOnly?>> AppealOpened = x => x.AppealDate;
  private static readonly Expression<Func<PropertyAppeal, DateOnly?>> AppealNext = x => x.DecisionDueDate;
  private static readonly Expression<Func<AgreementViolation, DateOnly?>> ViolationOpened = x => x.ViolationDate;
  private static readonly Expression<Func<AgreementViolation, DateOnly?>> ViolationNext = x => x.NoticeDeadline;

  public async Task<Result<GetLegalMattersQueryResult>> Handle(GetLegalMattersQuery query, CancellationToken cancellationToken)
  {
    var kinds = query.Kinds is { Count: > 0 } ? query.Kinds.ToHashSet() : Enum.GetValues<LegalMatterKind>().ToHashSet();
    var scope = RegisterScope.Create(context, query.PropertyId, query.TownId, query.Search);

    var rows = new List<Row>();
    if (kinds.Contains(LegalMatterKind.Encroachment)) rows.AddRange(await EncroachmentsAsync(query, scope, cancellationToken));
    if (kinds.Contains(LegalMatterKind.Case)) rows.AddRange(await CasesAsync(query, scope, cancellationToken));
    if (kinds.Contains(LegalMatterKind.Appeal)) rows.AddRange(await AppealsAsync(query, scope, cancellationToken));
    if (kinds.Contains(LegalMatterKind.Violation)) rows.AddRange(await ViolationsAsync(query, scope, cancellationToken));

    var codes = await read.PropertyCodesAsync(rows.Select(r => r.PropertyId), cancellationToken);
    string PropertyCode(Row r) => codes[r.PropertyId];

    var desc = query.SortDescending;
    var ordered = query.SortBy switch
    {
      LegalMatterSort.Opened => rows.NullsLast(x => x.Opened, desc),
      LegalMatterSort.Code => rows.ByText(x => x.Code, desc),
      LegalMatterSort.Property => rows.ByText(PropertyCode, desc),
      _ => rows.NullsLast(x => x.Next, desc),
    };

    // the id last: a notice no. or a case no. is not unique on its own, and a tie must page the same way every time
    var page = ordered.ThenBy(PropertyCode, StringComparer.Ordinal).ThenBy(x => x.Kind).ThenBy(x => x.Code, StringComparer.Ordinal).ThenBy(x => x.Id)
        .Page(query.Pagination);

    var properties = await read.PropertyHeadersAsync(page.Select(r => r.PropertyId), cancellationToken);
    var owners = await read.OwnerRefsAsync(page.Select(r => r.PartyOwnerId), cancellationToken);
    var caseParties = await CasePartiesAsync(page.Where(r => r.Kind == LegalMatterKind.Case).Select(r => r.Id), cancellationToken);
    var refs = await masters.Refs()
        .Add<EncroachmentStatus>(page.Where(r => r.Kind == LegalMatterKind.Encroachment).Select(r => r.StatusId))
        .Add<LitigationStatus>(page.Where(r => r.Kind == LegalMatterKind.Case).Select(r => r.StatusId))
        .LoadAsync(cancellationToken);

    var items = page.Select(r =>
    {
      var property = properties[r.PropertyId];
      var party = r.Kind == LegalMatterKind.Case
        ? caseParties.GetValueOrDefault(r.Id)
        : r.PartyName ?? (r.PartyOwnerId is null ? null : owners.GetValueOrDefault(r.PartyOwnerId)?.OwnerName);
      return new LegalMatterListItemDto(
        r.Kind, r.Id, r.Code, r.Title, property.Id, property.PropertyCode, property.PropertyName, property.Town,
        party, r.Court, r.FixedStatus ?? refs[r.StatusId].ToStatusRef(), r.Opened, r.Next, r.AreaSqFt, r.FineAmount, r.FineStatus, r.IsOpen);
    }).ToList();

    return Result<GetLegalMattersQueryResult>.Success(new GetLegalMattersQueryResult(new PaginatedResult<LegalMatterListItemDto>(
      query.Pagination.Pageindex, query.Pagination.PageSize, rows.Count, items)));
  }

  /// The other side of each case, as one line ("A, B"): GDA itself is not recorded as a party.
  private async Task<Dictionary<Guid, string>> CasePartiesAsync(IEnumerable<Guid> caseIds, CancellationToken cancellationToken)
  {
    var ids = caseIds.Select(LitigationId.Of).ToList();
    if (ids.Count == 0)
      return new Dictionary<Guid, string>();

    return (await context.Litigations.AsNoTracking().Include(x => x.Parties).Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken))
        .Where(x => x.Parties.Count > 0)
        .ToDictionary(x => x.Id.Value, x => string.Join(", ", x.Parties.Select(p => p.PartyName)));
  }

  private async Task<IEnumerable<Row>> EncroachmentsAsync(GetLegalMattersQuery query, RegisterScope scope, CancellationToken cancellationToken)
  {
    // an encroachment has no next date, so a next-date filter leaves none
    if (query.NextFrom is not null || query.NextTo is not null)
      return [];

    var rows = context.Encroachments.AsNoTracking();

    if (scope.Properties is { } allowed) rows = rows.Where(x => allowed.Contains(x.PropertyId));
    if (query.Open is { } open) rows = rows.Where(open ? OpenMatters.EncroachmentIsOpen : OpenMatters.Not(OpenMatters.EncroachmentIsOpen));
    if (scope.HasSearch)
    {
      var (upper, lower, properties, owners) = (scope.Upper!, scope.Lower!, scope.SearchProperties, scope.SearchOwners);
      rows = rows.Where(x => ((string)(object)x.EncroachmentNo).Contains(upper)
        || (x.Description != null && x.Description.ToLower().Contains(lower))
        || (x.EncroacherName != null && x.EncroacherName.ToLower().Contains(lower))
        || (x.EncroacherOwnerId != null && owners.Contains(x.EncroacherOwnerId))
        || properties.Contains(x.PropertyId));
    }

    rows = rows.InRange(EncroachmentOpened, query.OpenedFrom, query.OpenedTo);

    return (await rows.ToListAsync(cancellationToken)).Select(x => new Row(
      LegalMatterKind.Encroachment, x.Id.Value, x.EncroachmentNo.Value, x.Description, x.PropertyId, x.EncroacherName, x.EncroacherOwnerId,
      null, x.EncroachmentStatusId, null, x.DetectionDate, null, x.EncroachmentAreaBase, null, null, OpenMatters.IsOpenEncroachment(x)));
  }

  private async Task<IEnumerable<Row>> CasesAsync(GetLegalMattersQuery query, RegisterScope scope, CancellationToken cancellationToken)
  {
    var closed = await OpenMatters.ClosedCaseStatusIdsAsync(context, cancellationToken);
    var isOpen = OpenMatters.CaseIsOpen(closed);
    var rows = context.Litigations.AsNoTracking();

    if (scope.Properties is { } allowed) rows = rows.Where(x => allowed.Contains(x.PropertyId));
    if (query.Open is { } open) rows = rows.Where(open ? isOpen : OpenMatters.Not(isOpen));
    if (scope.HasSearch)
    {
      var (lower, properties) = (scope.Lower!, scope.SearchProperties);
      rows = rows.Where(x => x.CaseNo.ToLower().Contains(lower)
        || x.CaseTitle.ToLower().Contains(lower)
        || x.CourtAuthority.ToLower().Contains(lower)
        || x.Parties.Any(p => p.PartyName.ToLower().Contains(lower))
        || properties.Contains(x.PropertyId));
    }

    rows = rows.InRange(CaseOpened, query.OpenedFrom, query.OpenedTo).InRange(CaseNext, query.NextFrom, query.NextTo);

    // the parties are read for the page only (CasePartiesAsync)
    return (await rows.ToListAsync(cancellationToken)).Select(x => new Row(
      LegalMatterKind.Case, x.Id.Value, x.CaseNo, x.CaseTitle, x.PropertyId, null, null,
      x.CourtAuthority, x.LitigationStatusId, null, x.FilingDate, x.NextHearingDate, null, null, null, !closed.Contains(x.LitigationStatusId)));
  }

  private async Task<IEnumerable<Row>> AppealsAsync(GetLegalMattersQuery query, RegisterScope scope, CancellationToken cancellationToken)
  {
    var rows = context.Appeals.AsNoTracking();

    if (scope.Properties is { } allowed) rows = rows.Where(x => allowed.Contains(x.PropertyId));
    if (query.Open is { } open) rows = rows.Where(open ? OpenMatters.AppealIsOpen : OpenMatters.Not(OpenMatters.AppealIsOpen));
    if (scope.HasSearch)
    {
      var (upper, lower, properties, owners) = (scope.Upper!, scope.Lower!, scope.SearchProperties, scope.SearchOwners);
      rows = rows.Where(x => ((string)(object)x.AppealNo).Contains(upper)
        || x.AppealedOrderRef.ToLower().Contains(lower)
        || x.AppellateAuthority.ToLower().Contains(lower)
        || owners.Contains(x.AppellantOwnerId)
        || properties.Contains(x.PropertyId));
    }

    rows = rows.InRange(AppealOpened, query.OpenedFrom, query.OpenedTo).InRange(AppealNext, query.NextFrom, query.NextTo);

    return (await rows.ToListAsync(cancellationToken)).Select(x => new Row(
      LegalMatterKind.Appeal, x.Id.Value, x.AppealNo.Value, x.AppealedOrderRef, x.PropertyId, null, x.AppellantOwnerId,
      x.AppellateAuthority, null, x.AppealStatus.ToStatusRef(), x.AppealDate, x.DecisionDueDate, null, null, null, OpenMatters.IsOpenAppeal(x)));
  }

  private async Task<IEnumerable<Row>> ViolationsAsync(GetLegalMattersQuery query, RegisterScope scope, CancellationToken cancellationToken)
  {
    var rows = context.Violations.AsNoTracking();

    if (scope.Properties is { } allowed) rows = rows.Where(x => allowed.Contains(x.PropertyId));
    if (query.Open is { } open) rows = rows.Where(open ? OpenMatters.ViolationIsOpen : OpenMatters.Not(OpenMatters.ViolationIsOpen));
    if (scope.HasSearch)
    {
      var (lower, properties, owners) = (scope.Lower!, scope.SearchProperties, scope.SearchOwners);
      rows = rows.Where(x => (x.NoticeNo != null && x.NoticeNo.ToLower().Contains(lower))
        || x.ViolationDescription.ToLower().Contains(lower)
        || owners.Contains(x.ViolatorOwnerId)
        || properties.Contains(x.PropertyId));
    }

    rows = rows.InRange(ViolationOpened, query.OpenedFrom, query.OpenedTo).InRange(ViolationNext, query.NextFrom, query.NextTo);

    // a violation has no number of its own: the notice no. stands for it, else its id
    return (await rows.ToListAsync(cancellationToken)).Select(x => new Row(
      LegalMatterKind.Violation, x.Id.Value, x.NoticeNo ?? x.Id.Value.ToString(), x.ViolationDescription, x.PropertyId, null, x.ViolatorOwnerId,
      null, null, x.ViolationStatus.ToStatusRef(), x.ViolationDate, x.NoticeDeadline, null, x.FineAmount, x.FineStatus, OpenMatters.IsOpenViolation(x)));
  }
}
