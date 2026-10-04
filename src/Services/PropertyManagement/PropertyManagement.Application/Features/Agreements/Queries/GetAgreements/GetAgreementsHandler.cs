using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

/// Each kind is filtered in SQL on its own table, then the five are merged, ordered and paged in memory; property
/// names, parties and master values are read for the page only.
///
/// In force, per kind (read from the record's status code, matching the domain's own rules):
///   allotment   — ACTIVE or RESTORED (the statuses an allottee can be confirmed as owner in); CANCELLED,
///                 SURRENDERED, EXPIRED ... are not. Deactivated allotments (entered in error) are not listed.
///   lease       — ACTIVE or DRAFT (PropertyLease.IsInForce: a draft is agreed and has not ended); EXPIRED,
///                 RENEWED, TERMINATED and CANCELLED are not.
///   rental      — ACTIVE (PropertyRental.IsInForce); ENDED and CANCELLED are not.
///   outsourcing — ACTIVE (the only status a contract can still change in); EXPIRED and TERMINATED are not.
///   auction     — still open: neither AWARDED nor CANCELLED (the two statuses that close an auction).
///
/// Start / end date, per kind:
///   allotment   — effective date (else allotment date) / cancellation date while cancelled (by status: a restored
///                 allotment keeps its old cancellation date), else expiry date
///   lease, rental, outsourcing — start date / termination date when ended early, else the agreed end date
///   auction     — auction date (else announcement date) / award date
public class GetAgreementsHandler(IApplicationDbContext context, MasterLookup masters, PropertyReadService read)
  : IQueryHandler<GetAgreementsQuery, Result<GetAgreementsQueryResult>>
{
  private sealed record Row(
    AgreementKind Kind, Guid Id, string Code, PropertyId PropertyId, OwnerId? PartyId, MasterId TypeId, MasterId StatusId,
    DateOnly? Start, DateOnly? End, decimal? Amount, string? Frequency, decimal? Deposit, bool InForce);

  // start / end of each kind: translated to SQL for the date filters, compiled for the row itself
  private static readonly Expression<Func<PropertyAllotment, DateOnly?>> AllotmentStarts = x => x.EffectiveDate ?? x.AllotmentDate;
  private static Expression<Func<PropertyAllotment, DateOnly?>> AllotmentEnds(List<MasterId> cancelled) =>
      x => cancelled.Contains(x.AllotmentStatusId) ? x.CancellationDate : x.ExpiryDate;
  private static readonly Expression<Func<PropertyLease, DateOnly?>> LeaseStarts = x => x.LeaseStartDate;
  private static readonly Expression<Func<PropertyLease, DateOnly?>> LeaseEnds = x => x.TerminationDate ?? x.LeaseEndDate;
  private static readonly Expression<Func<PropertyRental, DateOnly?>> RentalStarts = x => x.RentalStartDate;
  private static readonly Expression<Func<PropertyRental, DateOnly?>> RentalEnds = x => x.TerminationDate ?? x.RentalEndDate;
  private static readonly Expression<Func<PropertyOutsourcing, DateOnly?>> ContractStarts = x => x.ContractStartDate;
  private static readonly Expression<Func<PropertyOutsourcing, DateOnly?>> ContractEnds = x => x.TerminationDate ?? x.ContractEndDate;
  private static readonly Expression<Func<PropertyAuction, DateOnly?>> AuctionStarts = x => x.AuctionDate ?? x.AnnouncementDate;
  private static readonly Expression<Func<PropertyAuction, DateOnly?>> AuctionEnds = x => x.AwardDate;

  private static readonly Func<PropertyAllotment, DateOnly?> AllotmentStartOf = AllotmentStarts.Compile();
  private static readonly Func<PropertyLease, DateOnly?> LeaseStartOf = LeaseStarts.Compile();
  private static readonly Func<PropertyLease, DateOnly?> LeaseEndOf = LeaseEnds.Compile();
  private static readonly Func<PropertyRental, DateOnly?> RentalStartOf = RentalStarts.Compile();
  private static readonly Func<PropertyRental, DateOnly?> RentalEndOf = RentalEnds.Compile();
  private static readonly Func<PropertyOutsourcing, DateOnly?> ContractStartOf = ContractStarts.Compile();
  private static readonly Func<PropertyOutsourcing, DateOnly?> ContractEndOf = ContractEnds.Compile();
  private static readonly Func<PropertyAuction, DateOnly?> AuctionStartOf = AuctionStarts.Compile();
  private static readonly Func<PropertyAuction, DateOnly?> AuctionEndOf = AuctionEnds.Compile();

  public async Task<Result<GetAgreementsQueryResult>> Handle(GetAgreementsQuery query, CancellationToken cancellationToken)
  {
    var kinds = query.Kinds is { Count: > 0 } ? query.Kinds.ToHashSet() : Enum.GetValues<AgreementKind>().ToHashSet();
    var scope = RegisterScope.Create(context, query.PropertyId, query.TownId, query.Search);
    var party = query.PartyId is { } partyId ? OwnerId.Of(partyId) : null;

    var rows = new List<Row>();
    if (kinds.Contains(AgreementKind.Allotment)) rows.AddRange(await AllotmentsAsync(query, scope, party, cancellationToken));
    if (kinds.Contains(AgreementKind.Lease)) rows.AddRange(await LeasesAsync(query, scope, party, cancellationToken));
    if (kinds.Contains(AgreementKind.Rental)) rows.AddRange(await RentalsAsync(query, scope, party, cancellationToken));
    if (kinds.Contains(AgreementKind.Outsourcing)) rows.AddRange(await OutsourcingsAsync(query, scope, party, cancellationToken));
    if (kinds.Contains(AgreementKind.Auction)) rows.AddRange(await AuctionsAsync(query, scope, party, cancellationToken));

    var codes = await read.PropertyCodesAsync(rows.Select(r => r.PropertyId), cancellationToken);
    string PropertyCode(Row r) => codes[r.PropertyId];

    var desc = query.SortDescending;
    var ordered = query.SortBy switch
    {
      AgreementSort.Starts => rows.NullsLast(x => x.Start, desc),
      AgreementSort.Code => rows.ByText(x => x.Code, desc),
      AgreementSort.Property => rows.ByText(PropertyCode, desc),
      AgreementSort.Amount => rows.NullsLast(x => x.Amount, desc),
      _ => rows.NullsLast(x => x.End, desc),
    };

    // the id last: rows that tie on everything else still fall on the same page every time
    var page = ordered.ThenBy(PropertyCode, StringComparer.Ordinal).ThenBy(x => x.Kind).ThenBy(x => x.Code, StringComparer.Ordinal).ThenBy(x => x.Id)
        .Page(query.Pagination);

    var properties = await read.PropertyHeadersAsync(page.Select(r => r.PropertyId), cancellationToken);
    var owners = await read.OwnerRefsAsync(page.Select(r => r.PartyId), cancellationToken);
    var refs = await masters.Refs()
        .Add<AllotmentType>(Ids(page, AgreementKind.Allotment, r => r.TypeId)).Add<AllotmentStatus>(Ids(page, AgreementKind.Allotment, r => r.StatusId))
        .Add<LeaseType>(Ids(page, AgreementKind.Lease, r => r.TypeId)).Add<LeaseStatus>(Ids(page, AgreementKind.Lease, r => r.StatusId))
        .Add<RentalType>(Ids(page, AgreementKind.Rental, r => r.TypeId)).Add<RentalStatus>(Ids(page, AgreementKind.Rental, r => r.StatusId))
        .Add<OutsourcingType>(Ids(page, AgreementKind.Outsourcing, r => r.TypeId)).Add<ContractStatus>(Ids(page, AgreementKind.Outsourcing, r => r.StatusId))
        .Add<AuctionType>(Ids(page, AgreementKind.Auction, r => r.TypeId)).Add<AuctionStatus>(Ids(page, AgreementKind.Auction, r => r.StatusId))
        .LoadAsync(cancellationToken);

    var items = page.Select(r =>
    {
      var property = properties[r.PropertyId];
      var owner = r.PartyId is null ? null : owners.GetValueOrDefault(r.PartyId);
      return new AgreementListItemDto(
        r.Kind, r.Id, r.Code, property.Id, property.PropertyCode, property.PropertyName, property.Town,
        owner?.Id, owner?.OwnerCode, owner?.OwnerName, refs[r.TypeId], refs[r.StatusId],
        r.Start, r.End, r.Amount, r.Frequency, r.Deposit, r.InForce);
    }).ToList();

    return Result<GetAgreementsQueryResult>.Success(new GetAgreementsQueryResult(new PaginatedResult<AgreementListItemDto>(
      query.Pagination.Pageindex, query.Pagination.PageSize, rows.Count, items)));
  }

  private async Task<IEnumerable<Row>> AllotmentsAsync(GetAgreementsQuery query, RegisterScope scope, OwnerId? party, CancellationToken cancellationToken)
  {
    var inForce = await StatusIdsAsync<AllotmentStatus>(s => s.Is(SystemMasterCodes.Active) || s.Is(SystemMasterCodes.Restored), cancellationToken);
    var cancelled = await StatusIdsAsync<AllotmentStatus>(s => s.Is(SystemMasterCodes.Cancelled), cancellationToken);
    var ends = AllotmentEnds(cancelled);
    var endOf = ends.Compile();
    var rows = context.Allotments.AsNoTracking().Where(x => x.IsActive);

    if (scope.Properties is { } allowed) rows = rows.Where(x => allowed.Contains(x.PropertyId));
    if (party is not null) rows = rows.Where(x => x.AllotteeOwnerId == party);
    if (query.InForce is { } wanted) rows = rows.Where(x => inForce.Contains(x.AllotmentStatusId) == wanted);
    if (scope.HasSearch)
    {
      var (upper, properties, owners) = (scope.Upper!, scope.SearchProperties, scope.SearchOwners);
      rows = rows.Where(x => ((string)(object)x.AllotmentNo).Contains(upper) || properties.Contains(x.PropertyId) || owners.Contains(x.AllotteeOwnerId));
    }

    rows = rows.InRange(AllotmentStarts, query.StartFrom, query.StartTo).InRange(ends, query.EndFrom, query.EndTo);

    return (await rows.ToListAsync(cancellationToken)).Select(x => new Row(
      AgreementKind.Allotment, x.Id.Value, x.AllotmentNo.Value, x.PropertyId, x.AllotteeOwnerId, x.AllotmentTypeId, x.AllotmentStatusId,
      AllotmentStartOf(x), endOf(x), null, null, null, inForce.Contains(x.AllotmentStatusId)));
  }

  private async Task<IEnumerable<Row>> LeasesAsync(GetAgreementsQuery query, RegisterScope scope, OwnerId? party, CancellationToken cancellationToken)
  {
    var inForce = await StatusIdsAsync<LeaseStatus>(s => s.Is(SystemMasterCodes.Active) || s.Is(SystemMasterCodes.Draft), cancellationToken);
    var rows = context.Leases.AsNoTracking();

    if (scope.Properties is { } allowed) rows = rows.Where(x => allowed.Contains(x.PropertyId));
    if (party is not null) rows = rows.Where(x => x.LesseeOwnerId == party);
    if (query.InForce is { } wanted) rows = rows.Where(x => inForce.Contains(x.LeaseStatusId) == wanted);
    if (scope.HasSearch)
    {
      var (upper, properties, owners) = (scope.Upper!, scope.SearchProperties, scope.SearchOwners);
      rows = rows.Where(x => ((string)(object)x.LeaseNo).Contains(upper) || properties.Contains(x.PropertyId) || owners.Contains(x.LesseeOwnerId));
    }

    rows = rows.InRange(LeaseStarts, query.StartFrom, query.StartTo).InRange(LeaseEnds, query.EndFrom, query.EndTo);

    return (await rows.ToListAsync(cancellationToken)).Select(x => new Row(
      AgreementKind.Lease, x.Id.Value, x.LeaseNo.Value, x.PropertyId, x.LesseeOwnerId, x.LeaseTypeId, x.LeaseStatusId,
      LeaseStartOf(x), LeaseEndOf(x), x.LeaseAmount, x.AmountFrequency?.ToString(), x.SecurityDeposit, inForce.Contains(x.LeaseStatusId)));
  }

  private async Task<IEnumerable<Row>> RentalsAsync(GetAgreementsQuery query, RegisterScope scope, OwnerId? party, CancellationToken cancellationToken)
  {
    var inForce = await StatusIdsAsync<RentalStatus>(s => s.Is(SystemMasterCodes.Active), cancellationToken);
    var rows = context.Rentals.AsNoTracking();

    if (scope.Properties is { } allowed) rows = rows.Where(x => allowed.Contains(x.PropertyId));
    if (party is not null) rows = rows.Where(x => x.TenantOwnerId == party);
    if (query.InForce is { } wanted) rows = rows.Where(x => inForce.Contains(x.RentalStatusId) == wanted);
    if (scope.HasSearch)
    {
      var (upper, properties, owners) = (scope.Upper!, scope.SearchProperties, scope.SearchOwners);
      rows = rows.Where(x => ((string)(object)x.RentalNo).Contains(upper) || properties.Contains(x.PropertyId) || owners.Contains(x.TenantOwnerId));
    }

    rows = rows.InRange(RentalStarts, query.StartFrom, query.StartTo).InRange(RentalEnds, query.EndFrom, query.EndTo);

    return (await rows.ToListAsync(cancellationToken)).Select(x => new Row(
      AgreementKind.Rental, x.Id.Value, x.RentalNo.Value, x.PropertyId, x.TenantOwnerId, x.RentalTypeId, x.RentalStatusId,
      RentalStartOf(x), RentalEndOf(x), x.RentAmount, x.RentFrequency.ToString(), x.SecurityDeposit, inForce.Contains(x.RentalStatusId)));
  }

  private async Task<IEnumerable<Row>> OutsourcingsAsync(GetAgreementsQuery query, RegisterScope scope, OwnerId? party, CancellationToken cancellationToken)
  {
    var inForce = await StatusIdsAsync<ContractStatus>(s => s.Is(SystemMasterCodes.Active), cancellationToken);
    var rows = context.Outsourcings.AsNoTracking();

    if (scope.Properties is { } allowed) rows = rows.Where(x => allowed.Contains(x.PropertyId));
    if (party is not null) rows = rows.Where(x => x.OutsourcedPartyOwnerId == party);
    if (query.InForce is { } wanted) rows = rows.Where(x => inForce.Contains(x.ContractStatusId) == wanted);
    if (scope.HasSearch)
    {
      var (upper, properties, owners) = (scope.Upper!, scope.SearchProperties, scope.SearchOwners);
      rows = rows.Where(x => ((string)(object)x.ContractNo).Contains(upper) || properties.Contains(x.PropertyId) || owners.Contains(x.OutsourcedPartyOwnerId));
    }

    rows = rows.InRange(ContractStarts, query.StartFrom, query.StartTo).InRange(ContractEnds, query.EndFrom, query.EndTo);

    // the performance guarantee is what a contractor deposits
    return (await rows.ToListAsync(cancellationToken)).Select(x => new Row(
      AgreementKind.Outsourcing, x.Id.Value, x.ContractNo.Value, x.PropertyId, x.OutsourcedPartyOwnerId, x.OutsourcingTypeId, x.ContractStatusId,
      ContractStartOf(x), ContractEndOf(x), x.ContractAmount, x.AmountFrequency?.ToString(), x.PerformanceGuarantee, inForce.Contains(x.ContractStatusId)));
  }

  private async Task<IEnumerable<Row>> AuctionsAsync(GetAgreementsQuery query, RegisterScope scope, OwnerId? party, CancellationToken cancellationToken)
  {
    var inForce = await StatusIdsAsync<AuctionStatus>(s => !s.Is(SystemMasterCodes.Awarded) && !s.Is(SystemMasterCodes.Cancelled), cancellationToken);
    var rows = context.Auctions.AsNoTracking();

    if (scope.Properties is { } allowed) rows = rows.Where(x => allowed.Contains(x.PropertyId));
    if (party is not null) rows = rows.Where(x => x.SuccessfulBidderOwnerId == party);
    if (query.InForce is { } wanted) rows = rows.Where(x => inForce.Contains(x.AuctionStatusId) == wanted);
    if (scope.HasSearch)
    {
      var (upper, properties, owners) = (scope.Upper!, scope.SearchProperties, scope.SearchOwners);
      rows = rows.Where(x => ((string)(object)x.AuctionNo).Contains(upper) || properties.Contains(x.PropertyId)
        || (x.SuccessfulBidderOwnerId != null && owners.Contains(x.SuccessfulBidderOwnerId)));
    }

    rows = rows.InRange(AuctionStarts, query.StartFrom, query.StartTo).InRange(AuctionEnds, query.EndFrom, query.EndTo);

    // the amount is the winning bid once awarded, the reserve price before
    return (await rows.ToListAsync(cancellationToken)).Select(x => new Row(
      AgreementKind.Auction, x.Id.Value, x.AuctionNo.Value, x.PropertyId, x.SuccessfulBidderOwnerId, x.AuctionTypeId, x.AuctionStatusId,
      AuctionStartOf(x), AuctionEndOf(x), x.WinningBidAmount ?? x.BaseReservePrice, null, null, inForce.Contains(x.AuctionStatusId)));
  }

  /// The ids of one status table's rows that satisfy the rule (status tables are tiny, so they are read whole).
  private async Task<List<MasterId>> StatusIdsAsync<T>(Func<T, bool> rule, CancellationToken cancellationToken) where T : MasterData =>
      (await context.Set<T>().AsNoTracking().ToListAsync(cancellationToken)).Where(rule).Select(s => s.Id).ToList();

  private static IEnumerable<MasterId?> Ids(IEnumerable<Row> rows, AgreementKind kind, Func<Row, MasterId> pick) =>
      rows.Where(r => r.Kind == kind).Select(r => (MasterId?)pick(r));
}
