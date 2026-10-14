using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

/// Each kind is filtered in SQL on its own table, then the four are merged, filtered on what only exists per row
/// (status code, next date against today), ordered and paged in memory; property names and parties are read for
/// the page only.
/// Open / closed follows OpenMatters. Dates, per kind:
///   encroachment — opened: detection date; next: none (the record holds no notice deadline)
///   case         — opened: filing date; next: next hearing date
///   appeal       — opened: appeal date; next: decision due date (appeal date + 120 days, Act s.32)
///   violation    — opened: violation date; next: the deadline given in the notice
/// A violation's code is its notice no.; without one, "{agreement code} · violation #{occurrence no.}".
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
    var party = query.PartyId is { } partyId ? OwnerId.Of(partyId) : null;
    var court = string.IsNullOrWhiteSpace(query.Court) ? null : query.Court.Trim().ToLowerInvariant();

    var rows = new List<Row>();
    if (kinds.Contains(LegalMatterKind.Encroachment)) rows.AddRange(await EncroachmentsAsync(query, scope, party, court, cancellationToken));
    if (kinds.Contains(LegalMatterKind.Case)) rows.AddRange(await CasesAsync(query, scope, party, court, cancellationToken));
    if (kinds.Contains(LegalMatterKind.Appeal)) rows.AddRange(await AppealsAsync(query, scope, party, court, cancellationToken));
    if (kinds.Contains(LegalMatterKind.Violation)) rows.AddRange(await ViolationsAsync(query, scope, party, court, cancellationToken));

    // the statuses of every row (a handful of master values), for the status filter and sort
    var refs = await masters.Refs()
        .Add<EncroachmentStatus>(rows.Where(r => r.Kind == LegalMatterKind.Encroachment).Select(r => r.StatusId))
        .Add<LitigationStatus>(rows.Where(r => r.Kind == LegalMatterKind.Case).Select(r => r.StatusId))
        .LoadAsync(cancellationToken);
    StatusRef? StatusOf(Row r) => r.FixedStatus ?? refs[r.StatusId].ToStatusRef();

    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    IEnumerable<Row> filtered = rows;
    if (query.Statuses is { Count: > 0 })
    {
      var wanted = query.Statuses.Select(StatusKey).ToHashSet();
      filtered = filtered.Where(r => StatusOf(r) is { } status && wanted.Contains(StatusKey(status.Code)));
    }
    if (query.NextWithinDays is { } days)
    {
      var until = today.AddDays(days);
      filtered = filtered.Where(r => r.IsOpen && r.Next is { } next && next >= today && next <= until);
    }
    if (query.HearingOverdue is { } overdue) filtered = filtered.Where(r => (r.IsOpen && r.Next is { } next && next < today) == overdue);
    rows = filtered.ToList();

    var codes = await read.PropertyCodesAsync(rows.Select(r => r.PropertyId), cancellationToken);
    string PropertyCode(Row r) => codes[r.PropertyId];

    var desc = query.SortDescending;
    var ordered = query.SortBy switch
    {
      LegalMatterSort.Opened => rows.NullsLast(x => x.Opened, desc),
      LegalMatterSort.Code => rows.ByText(x => x.Code, desc),
      LegalMatterSort.Property => rows.ByText(PropertyCode, desc),
      LegalMatterSort.Kind => rows.ByText(x => x.Kind.ToString(), desc),
      LegalMatterSort.Title => rows.ByText(x => x.Title ?? string.Empty, desc),
      LegalMatterSort.Status => rows.ByText(x => StatusOf(x)?.Name ?? string.Empty, desc),
      _ => rows.NullsLast(x => x.Next, desc),
    };

    // the id last: a notice no. or a case no. is not unique on its own, and a tie must page the same way every time
    var page = ordered.ThenBy(PropertyCode, StringComparer.Ordinal).ThenBy(x => x.Kind).ThenBy(x => x.Code, StringComparer.Ordinal).ThenBy(x => x.Id)
        .Page(query.Pagination);

    var properties = await read.PropertyHeadersAsync(page.Select(r => r.PropertyId), cancellationToken);
    var owners = await read.OwnerRefsAsync(page.Select(r => r.PartyOwnerId), cancellationToken);
    var caseParties = await CasePartiesAsync(page.Where(r => r.Kind == LegalMatterKind.Case).Select(r => r.Id), cancellationToken);

    var items = page.Select(r =>
    {
      var property = properties[r.PropertyId];
      var partyName = r.Kind == LegalMatterKind.Case
        ? caseParties.GetValueOrDefault(r.Id)
        : r.PartyName ?? (r.PartyOwnerId is null ? null : owners.GetValueOrDefault(r.PartyOwnerId)?.OwnerName);
      return new LegalMatterListItemDto(
        r.Kind, r.Id, r.Code, r.Title, property.Id, property.PropertyCode, property.PropertyName, property.Town,
        partyName, r.Court, StatusOf(r), r.Opened, r.Next, r.AreaSqFt, r.FineAmount, r.FineStatus, r.IsOpen);
    }).ToList();

    return Result<GetLegalMattersQueryResult>.Success(new GetLegalMattersQueryResult(new PaginatedResult<LegalMatterListItemDto>(
      query.Pagination.Pageindex, query.Pagination.PageSize, rows.Count, items)));
  }

  /// Status codes compared without case or underscores: UNDER_HEARING, UnderHearing and underhearing are the same.
  private static string StatusKey(string code) => code.Replace("_", string.Empty).Trim().ToUpperInvariant();

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

  private async Task<IEnumerable<Row>> EncroachmentsAsync(GetLegalMattersQuery query, RegisterScope scope, OwnerId? party, string? court, CancellationToken cancellationToken)
  {
    // an encroachment has no next date and no court, so a next-date or court filter leaves none
    if (query.NextFrom is not null || query.NextTo is not null || query.NextWithinDays is not null || query.HearingOverdue == true || court is not null)
      return [];

    var rows = context.Encroachments.AsNoTracking();

    if (scope.Properties is { } allowed) rows = rows.Where(x => allowed.Contains(x.PropertyId));
    if (party is not null) rows = rows.Where(x => x.EncroacherOwnerId == party);
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

  private async Task<IEnumerable<Row>> CasesAsync(GetLegalMattersQuery query, RegisterScope scope, OwnerId? party, string? court, CancellationToken cancellationToken)
  {
    var closed = await OpenMatters.ClosedCaseStatusIdsAsync(context, cancellationToken);
    var isOpen = OpenMatters.CaseIsOpen(closed);
    var rows = context.Litigations.AsNoTracking();

    if (scope.Properties is { } allowed) rows = rows.Where(x => allowed.Contains(x.PropertyId));
    if (party is not null) rows = rows.Where(x => x.Parties.Any(p => p.PartyOwnerId == party));
    if (court is not null) rows = rows.Where(x => x.CourtAuthority.ToLower().Contains(court));
    if (query.Open is { } open) rows = rows.Where(open ? isOpen : OpenMatters.Not(isOpen));
    if (scope.HasSearch)
    {
      var (lower, properties, owners) = (scope.Lower!, scope.SearchProperties, scope.SearchOwners);
      rows = rows.Where(x => x.CaseNo.ToLower().Contains(lower)
        || x.CaseTitle.ToLower().Contains(lower)
        || x.CourtAuthority.ToLower().Contains(lower)
        || x.Parties.Any(p => p.PartyName.ToLower().Contains(lower) || (p.PartyOwnerId != null && owners.Contains(p.PartyOwnerId)))
        || properties.Contains(x.PropertyId));
    }

    rows = rows.InRange(CaseOpened, query.OpenedFrom, query.OpenedTo).InRange(CaseNext, query.NextFrom, query.NextTo);

    // the parties are read for the page only (CasePartiesAsync)
    return (await rows.ToListAsync(cancellationToken)).Select(x => new Row(
      LegalMatterKind.Case, x.Id.Value, x.CaseNo, x.CaseTitle, x.PropertyId, null, null,
      x.CourtAuthority, x.LitigationStatusId, null, x.FilingDate, x.NextHearingDate, null, null, null, !closed.Contains(x.LitigationStatusId)));
  }

  private async Task<IEnumerable<Row>> AppealsAsync(GetLegalMattersQuery query, RegisterScope scope, OwnerId? party, string? court, CancellationToken cancellationToken)
  {
    var rows = context.Appeals.AsNoTracking();

    if (scope.Properties is { } allowed) rows = rows.Where(x => allowed.Contains(x.PropertyId));
    if (party is not null) rows = rows.Where(x => x.AppellantOwnerId == party);
    if (court is not null) rows = rows.Where(x => x.AppellateAuthority.ToLower().Contains(court));
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

  private async Task<IEnumerable<Row>> ViolationsAsync(GetLegalMattersQuery query, RegisterScope scope, OwnerId? party, string? court, CancellationToken cancellationToken)
  {
    // a violation is not before a court
    if (court is not null)
      return [];

    var rows = context.Violations.AsNoTracking();

    if (scope.Properties is { } allowed) rows = rows.Where(x => allowed.Contains(x.PropertyId));
    if (party is not null) rows = rows.Where(x => x.ViolatorOwnerId == party);
    if (query.Open is { } open) rows = rows.Where(open ? OpenMatters.ViolationIsOpen : OpenMatters.Not(OpenMatters.ViolationIsOpen));
    if (scope.HasSearch && ViolationLabelPattern.Match(scope.Lower!) is { Success: true } label)
    {
      // the label the list shows for a violation without a notice no.: "LSE-00077 · violation #1" (or "violation #1")
      var occurrence = int.Parse(label.Groups["n"].Value);
      rows = rows.Where(x => x.OccurrenceNo == occurrence);
      if (label.Groups["code"].Success)
      {
        var code = label.Groups["code"].Value.Trim().ToUpperInvariant();
        var leases = context.Leases.AsNoTracking().Where(l => ((string)(object)l.LeaseNo).Contains(code)).Select(l => l.Id);
        var rentals = context.Rentals.AsNoTracking().Where(r => ((string)(object)r.RentalNo).Contains(code)).Select(r => r.Id);
        var transfers = context.Transfers.AsNoTracking().Where(t => ((string)(object)t.TransferNo).Contains(code)).Select(t => t.Id);
        rows = rows.Where(x => (x.LeaseId != null && leases.Contains(x.LeaseId))
          || (x.RentalId != null && rentals.Contains(x.RentalId))
          || (x.TransferId != null && transfers.Contains(x.TransferId)));
      }
    }
    else if (scope.HasSearch)
    {
      // the agreement's code too: a violation without a notice no. is known by it
      var (upper, lower, properties, owners) = (scope.Upper!, scope.Lower!, scope.SearchProperties, scope.SearchOwners);
      var leases = context.Leases.AsNoTracking().Where(l => ((string)(object)l.LeaseNo).Contains(upper)).Select(l => l.Id);
      var rentals = context.Rentals.AsNoTracking().Where(r => ((string)(object)r.RentalNo).Contains(upper)).Select(r => r.Id);
      var transfers = context.Transfers.AsNoTracking().Where(t => ((string)(object)t.TransferNo).Contains(upper)).Select(t => t.Id);
      rows = rows.Where(x => (x.NoticeNo != null && x.NoticeNo.ToLower().Contains(lower))
        || x.ViolationDescription.ToLower().Contains(lower)
        || owners.Contains(x.ViolatorOwnerId)
        || properties.Contains(x.PropertyId)
        || (x.LeaseId != null && leases.Contains(x.LeaseId))
        || (x.RentalId != null && rentals.Contains(x.RentalId))
        || (x.TransferId != null && transfers.Contains(x.TransferId)));
    }

    rows = rows.InRange(ViolationOpened, query.OpenedFrom, query.OpenedTo).InRange(ViolationNext, query.NextFrom, query.NextTo);

    var list = await rows.ToListAsync(cancellationToken);
    var agreements = await AgreementCodesAsync(list.Where(x => x.NoticeNo is null).ToList(), cancellationToken);

    // a violation has no number of its own: the notice no. stands for it, else the agreement and the occurrence
    return list.Select(x => new Row(
      LegalMatterKind.Violation, x.Id.Value, x.NoticeNo ?? ViolationLabel(x, agreements), x.ViolationDescription, x.PropertyId, null, x.ViolatorOwnerId,
      null, null, x.ViolationStatus.ToStatusRef(), x.ViolationDate, x.NoticeDeadline, null, x.FineAmount, x.FineStatus, OpenMatters.IsOpenViolation(x)));
  }

  /// "lse-00077 · violation #1" or "violation #1" (the search text arrives lower-cased).
  private static readonly System.Text.RegularExpressions.Regex ViolationLabelPattern =
    new(@"^(?:(?<code>[a-z0-9/_-]+)\s*\u00b7?\s*)?violation\s*#\s*(?<n>\d{1,4})$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

  private static string ViolationLabel(AgreementViolation x, IReadOnlyDictionary<Guid, string> agreements)
  {
    var agreementId = x.LeaseId?.Value ?? x.RentalId?.Value ?? x.TransferId?.Value;
    var agreement = agreementId is { } id ? agreements.GetValueOrDefault(id) : null;
    return $"{agreement ?? "Agreement"} · violation #{x.OccurrenceNo}";
  }

  /// The lease / rental / transfer codes of the violations, keyed by the agreement's id.
  private async Task<Dictionary<Guid, string>> AgreementCodesAsync(List<AgreementViolation> violations, CancellationToken cancellationToken)
  {
    var codes = new Dictionary<Guid, string>();

    var leaseIds = violations.Where(v => v.LeaseId is not null).Select(v => v.LeaseId!).Distinct().ToList();
    if (leaseIds.Count > 0)
      foreach (var l in await context.Leases.AsNoTracking().Where(l => leaseIds.Contains(l.Id)).Select(l => new { l.Id, l.LeaseNo }).ToListAsync(cancellationToken))
        codes[l.Id.Value] = l.LeaseNo.Value;

    var rentalIds = violations.Where(v => v.RentalId is not null).Select(v => v.RentalId!).Distinct().ToList();
    if (rentalIds.Count > 0)
      foreach (var r in await context.Rentals.AsNoTracking().Where(r => rentalIds.Contains(r.Id)).Select(r => new { r.Id, r.RentalNo }).ToListAsync(cancellationToken))
        codes[r.Id.Value] = r.RentalNo.Value;

    var transferIds = violations.Where(v => v.TransferId is not null).Select(v => v.TransferId!).Distinct().ToList();
    if (transferIds.Count > 0)
      foreach (var t in await context.Transfers.AsNoTracking().Where(t => transferIds.Contains(t.Id)).Select(t => new { t.Id, t.TransferNo }).ToListAsync(cancellationToken))
        codes[t.Id.Value] = t.TransferNo.Value;

    return codes;
  }
}
