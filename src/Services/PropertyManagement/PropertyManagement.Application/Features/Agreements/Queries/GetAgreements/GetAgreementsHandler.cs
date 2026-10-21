using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

/// Each kind is filtered in SQL on its own table, then the five are merged, filtered on what only exists per row
/// (annual amount, ends within, overdue), ordered and paged in memory; property names and parties are read for the
/// page only (for every row when sorting by party).
///
/// In force, per kind: see InForce (allotment ACTIVE or RESTORED, lease ACTIVE, rental and contract ACTIVE, auction
/// PLANNED / ANNOUNCED / CONDUCTED). Deactivated allotments (entered in error) are not listed.
///
/// Start / end date, per kind:
///   allotment   — effective date (else allotment date) / cancellation date while cancelled (by status: a restored
///                 allotment keeps its old cancellation date), else expiry date
///   lease, rental, outsourcing — start date / termination date when ended early, else the agreed end date
///   auction     — auction date (else announcement date) / award date
///
/// Amount, per kind (AnnualAmount is the yearly equivalent the amount filters and sort use):
///   lease, outsourcing — the amount; annual = monthly x 12, annual x 1, one-time as it is
///   rental      — the rent; annual = monthly x 12, quarterly x 4, annual x 1
///   auction     — the winning bid once awarded, the reserve price before; annual = as it is (a one-off sale). An auction
///                 is a sale being run (PLANNED / ANNOUNCED / CONDUCTED), never an agreement in force.
///   allotment   — none
public class GetAgreementsHandler(IApplicationDbContext context, MasterLookup masters, PropertyReadService read)
  : IQueryHandler<GetAgreementsQuery, Result<GetAgreementsQueryResult>>
{
  private sealed record Row(
    AgreementKind Kind, Guid Id, string Code, PropertyId PropertyId, OwnerId? PartyId, MasterId TypeId, MasterId StatusId,
    DateOnly? Start, DateOnly? End, decimal? Amount, string? Frequency, decimal? AnnualAmount, decimal? Deposit, bool InForce,
    decimal? CurrentAnnualAmount = null, MoneyDirection? Direction = null);

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
    var today = PakistanDate.Today;
    var scope = RegisterScope.Create(context, query.PropertyId, query.TownId, query.Search);
    var filter = new Filter(
      query.PartyId is { } partyId ? OwnerId.Of(partyId) : null,
      query.TypeId is { } typeId ? MasterId.Of(typeId) : null,
      query.Statuses is { Count: > 0 } ? query.Statuses.Select(s => s.Trim().ToUpperInvariant()).ToList() : null);

    var rows = new List<Row>();
    if (kinds.Contains(AgreementKind.Allotment)) rows.AddRange(await AllotmentsAsync(query, scope, filter, today, cancellationToken));
    if (kinds.Contains(AgreementKind.Lease)) rows.AddRange(await LeasesAsync(query, scope, filter, today, cancellationToken));
    if (kinds.Contains(AgreementKind.Rental)) rows.AddRange(await RentalsAsync(query, scope, filter, today, cancellationToken));
    if (kinds.Contains(AgreementKind.Outsourcing)) rows.AddRange(await OutsourcingsAsync(query, scope, filter, today, cancellationToken));
    if (kinds.Contains(AgreementKind.Auction)) rows.AddRange(await AuctionsAsync(query, scope, filter, cancellationToken));

    // what only exists per row: the annual amount, the money direction and the end date against today
    IEnumerable<Row> filtered = rows;
    if (query.Direction is { } direction) filtered = filtered.Where(r => r.Direction == direction);
    // allottees not (yet) confirmed as owners: only allotments have that question
    if (query.Unconfirmed == true) filtered = filtered.Where(r => r.Kind == AgreementKind.Allotment);
    if (query.AmountMin is { } amountMin) filtered = filtered.Where(r => r.AnnualAmount >= amountMin);
    if (query.AmountMax is { } amountMax) filtered = filtered.Where(r => r.AnnualAmount <= amountMax);
    if (query.EndsWithinDays is { } days)
    {
      var until = today.AddDays(days);
      filtered = filtered.Where(r => r.InForce && r.End is { } end && end >= today && end <= until);
    }
    if (query.Overdue is { } overdue) filtered = filtered.Where(r => (r.InForce && r.End is { } end && end < today) == overdue);
    rows = filtered.ToList();

    var codes = await read.PropertyCodesAsync(rows.Select(r => r.PropertyId), cancellationToken);
    string PropertyCode(Row r) => codes[r.PropertyId];

    // types and statuses of every row (a handful of master values), so the list can sort on the status name
    var refs = await RefsAsync(rows, cancellationToken);
    string StatusName(Row r) => refs[r.StatusId]?.Name ?? string.Empty;

    var desc = query.SortDescending;
    var parties = query.SortBy == AgreementSort.Party
      ? await read.OwnerRefsAsync(rows.Select(r => r.PartyId), cancellationToken)
      : new Dictionary<OwnerId, OwnerRef>();
    string PartyName(Row r) => r.PartyId is null ? string.Empty : parties.GetValueOrDefault(r.PartyId)?.OwnerName ?? string.Empty;

    var ordered = query.SortBy switch
    {
      AgreementSort.Starts => rows.NullsLast(x => x.Start, desc),
      AgreementSort.Code => rows.ByText(x => x.Code, desc),
      AgreementSort.Property => rows.ByText(PropertyCode, desc),
      AgreementSort.Amount => rows.NullsLast(x => x.AnnualAmount, desc),
      AgreementSort.Kind => rows.ByText(x => x.Kind.ToString(), desc),
      // rows without a party (an auction not yet awarded) last
      AgreementSort.Party => desc
        ? rows.OrderBy(x => x.PartyId is null).ThenByDescending(PartyName, StringComparer.OrdinalIgnoreCase)
        : rows.OrderBy(x => x.PartyId is null).ThenBy(PartyName, StringComparer.OrdinalIgnoreCase),
      AgreementSort.Status => rows.ByText(StatusName, desc),
      _ => rows.NullsLast(x => x.End, desc),
    };

    // the id last: rows that tie on everything else still fall on the same page every time
    var page = ordered.ThenBy(PropertyCode, StringComparer.Ordinal).ThenBy(x => x.Kind).ThenBy(x => x.Code, StringComparer.Ordinal).ThenBy(x => x.Id)
        .Page(query.Pagination);

    var properties = await read.PropertyHeadersAsync(page.Select(r => r.PropertyId), cancellationToken);
    var owners = await read.OwnerRefsAsync(page.Select(r => r.PartyId), cancellationToken);

    var items = page.Select(r =>
    {
      var property = properties[r.PropertyId];
      var owner = r.PartyId is null ? null : owners.GetValueOrDefault(r.PartyId);
      return new AgreementListItemDto(
        r.Kind, r.Id, r.Code, property.Id, property.PropertyCode, property.PropertyName, property.Town,
        owner?.Id, owner?.OwnerCode, owner?.OwnerName, refs[r.TypeId], refs[r.StatusId],
        r.Start, r.End, r.Amount, r.Frequency, r.AnnualAmount, r.Deposit, r.InForce, r.CurrentAnnualAmount ?? r.AnnualAmount, r.Direction);
    }).ToList();

    return Result<GetAgreementsQueryResult>.Success(new GetAgreementsQueryResult(new PaginatedResult<AgreementListItemDto>(
      query.Pagination.Pageindex, query.Pagination.PageSize, rows.Count, items)));
  }

  /// The filters every kind applies in SQL: the party, the type, and the status codes (resolved per status table).
  private sealed record Filter(OwnerId? Party, MasterId? TypeId, List<string>? StatusCodes);

  /// In force: a current status (ACTIVE / RESTORED) and already in effect (effective date, else allotment date, on or
  /// before today).
  private async Task<IEnumerable<Row>> AllotmentsAsync(GetAgreementsQuery query, RegisterScope scope, Filter filter, DateOnly today, CancellationToken cancellationToken)
  {
    var inForce = await InForce.StatusIdsAsync<AllotmentStatus>(context, InForce.AllotmentStatuses, cancellationToken);
    var cancelled = await InForce.StatusIdsAsync<AllotmentStatus>(context, [SystemMasterCodes.Cancelled], cancellationToken);
    var ends = AllotmentEnds(cancelled);
    var endOf = ends.Compile();
    var rows = context.Allotments.AsNoTracking().Where(x => x.IsActive);

    if (scope.Properties is { } allowed) rows = rows.Where(x => allowed.Contains(x.PropertyId));
    if (filter.Party is { } party) rows = rows.Where(x => x.AllotteeOwnerId == party);
    if (filter.TypeId is { } type) rows = rows.Where(x => x.AllotmentTypeId == type);
    if (filter.StatusCodes is { } codes)
    {
      var statuses = await InForce.StatusIdsAsync<AllotmentStatus>(context, codes, cancellationToken);
      rows = rows.Where(x => statuses.Contains(x.AllotmentStatusId));
    }
    if (query.InForce is { } wanted)
      rows = rows.Where(x => (inForce.Contains(x.AllotmentStatusId) && (x.EffectiveDate ?? x.AllotmentDate) <= today) == wanted);
    if (query.Unconfirmed is { } unconfirmed)
    {
      // confirmed: an ownership row acquired through the allotment since it was last put in force
      var confirmations = context.Ownerships.AsNoTracking().Where(o => o.AcquiredViaAllotmentId != null);
      rows = rows.Where(x => !confirmations.Any(o => o.AcquiredViaAllotmentId == x.Id && (x.RestorationDate == null || o.EffectiveFrom >= x.RestorationDate)) == unconfirmed);
    }
    if (scope.HasSearch)
    {
      var (upper, properties, owners) = (scope.Upper!, scope.SearchProperties, scope.SearchOwners);
      rows = rows.Where(x => ((string)(object)x.AllotmentNo).Contains(upper) || properties.Contains(x.PropertyId) || owners.Contains(x.AllotteeOwnerId));
    }

    rows = rows.InRange(AllotmentStarts, query.StartFrom, query.StartTo).InRange(ends, query.EndFrom, query.EndTo);

    return (await rows.ToListAsync(cancellationToken)).Select(x => new Row(
      AgreementKind.Allotment, x.Id.Value, x.AllotmentNo.Value, x.PropertyId, x.AllotteeOwnerId, x.AllotmentTypeId, x.AllotmentStatusId,
      AllotmentStartOf(x), endOf(x), null, null, null, null, inForce.Contains(x.AllotmentStatusId) && x.TakesEffectOn <= today));
  }

  private async Task<IEnumerable<Row>> LeasesAsync(GetAgreementsQuery query, RegisterScope scope, Filter filter, DateOnly today, CancellationToken cancellationToken)
  {
    var inForce = await InForce.StatusIdsAsync<LeaseStatus>(context, InForce.LeaseStatuses, cancellationToken);
    var rows = context.Leases.AsNoTracking();

    if (scope.Properties is { } allowed) rows = rows.Where(x => allowed.Contains(x.PropertyId));
    if (filter.Party is { } party) rows = rows.Where(x => x.LesseeOwnerId == party);
    if (filter.TypeId is { } type) rows = rows.Where(x => x.LeaseTypeId == type);
    if (filter.StatusCodes is { } codes)
    {
      var statuses = await InForce.StatusIdsAsync<LeaseStatus>(context, codes, cancellationToken);
      rows = rows.Where(x => statuses.Contains(x.LeaseStatusId));
    }
    if (query.InForce is { } wanted) rows = rows.Where(x => (inForce.Contains(x.LeaseStatusId) && x.LeaseStartDate <= today) == wanted);
    if (scope.HasSearch)
    {
      var (upper, properties, owners) = (scope.Upper!, scope.SearchProperties, scope.SearchOwners);
      rows = rows.Where(x => ((string)(object)x.LeaseNo).Contains(upper) || properties.Contains(x.PropertyId) || owners.Contains(x.LesseeOwnerId));
    }

    rows = rows.InRange(LeaseStarts, query.StartFrom, query.StartTo).InRange(LeaseEnds, query.EndFrom, query.EndTo);

    return (await rows.ToListAsync(cancellationToken)).Select(x => new Row(
      AgreementKind.Lease, x.Id.Value, x.LeaseNo.Value, x.PropertyId, x.LesseeOwnerId, x.LeaseTypeId, x.LeaseStatusId,
      LeaseStartOf(x), LeaseEndOf(x), x.LeaseAmount, x.AmountFrequency?.ToString(), Annual(x.LeaseAmount, x.AmountFrequency), x.SecurityDeposit,
      inForce.Contains(x.LeaseStatusId) && x.LeaseStartDate <= today, Direction: MoneyDirection.Receivable));
  }

  private async Task<IEnumerable<Row>> RentalsAsync(GetAgreementsQuery query, RegisterScope scope, Filter filter, DateOnly today, CancellationToken cancellationToken)
  {
    var inForce = await InForce.StatusIdsAsync<RentalStatus>(context, InForce.RentalStatuses, cancellationToken);
    var rows = context.Rentals.AsNoTracking();

    if (scope.Properties is { } allowed) rows = rows.Where(x => allowed.Contains(x.PropertyId));
    if (filter.Party is { } party) rows = rows.Where(x => x.TenantOwnerId == party);
    if (filter.TypeId is { } type) rows = rows.Where(x => x.RentalTypeId == type);
    if (filter.StatusCodes is { } codes)
    {
      var statuses = await InForce.StatusIdsAsync<RentalStatus>(context, codes, cancellationToken);
      rows = rows.Where(x => statuses.Contains(x.RentalStatusId));
    }
    if (query.InForce is { } wanted) rows = rows.Where(x => (inForce.Contains(x.RentalStatusId) && x.RentalStartDate <= today) == wanted);
    if (scope.HasSearch)
    {
      var (upper, properties, owners) = (scope.Upper!, scope.SearchProperties, scope.SearchOwners);
      rows = rows.Where(x => ((string)(object)x.RentalNo).Contains(upper) || properties.Contains(x.PropertyId) || owners.Contains(x.TenantOwnerId));
    }

    rows = rows.InRange(RentalStarts, query.StartFrom, query.StartTo).InRange(RentalEnds, query.EndFrom, query.EndTo);

    return (await rows.ToListAsync(cancellationToken)).Select(x => new Row(
      AgreementKind.Rental, x.Id.Value, x.RentalNo.Value, x.PropertyId, x.TenantOwnerId, x.RentalTypeId, x.RentalStatusId,
      RentalStartOf(x), RentalEndOf(x), x.RentAmount, x.RentFrequency.ToString(), Annual(x.RentAmount, x.RentFrequency), x.SecurityDeposit,
      inForce.Contains(x.RentalStatusId) && x.RentalStartDate <= today, Annual(x.CurrentRent(today), x.RentFrequency), MoneyDirection.Receivable));
  }

  private async Task<IEnumerable<Row>> OutsourcingsAsync(GetAgreementsQuery query, RegisterScope scope, Filter filter, DateOnly today, CancellationToken cancellationToken)
  {
    var inForce = await InForce.StatusIdsAsync<ContractStatus>(context, InForce.ContractStatuses, cancellationToken);
    var rows = context.Outsourcings.AsNoTracking();

    if (scope.Properties is { } allowed) rows = rows.Where(x => allowed.Contains(x.PropertyId));
    if (filter.Party is { } party) rows = rows.Where(x => x.OutsourcedPartyOwnerId == party);
    if (filter.TypeId is { } type) rows = rows.Where(x => x.OutsourcingTypeId == type);
    if (filter.StatusCodes is { } codes)
    {
      var statuses = await InForce.StatusIdsAsync<ContractStatus>(context, codes, cancellationToken);
      rows = rows.Where(x => statuses.Contains(x.ContractStatusId));
    }
    if (query.InForce is { } wanted) rows = rows.Where(x => (inForce.Contains(x.ContractStatusId) && x.ContractStartDate <= today) == wanted);
    if (scope.HasSearch)
    {
      var (upper, properties, owners) = (scope.Upper!, scope.SearchProperties, scope.SearchOwners);
      rows = rows.Where(x => ((string)(object)x.ContractNo).Contains(upper) || properties.Contains(x.PropertyId) || owners.Contains(x.OutsourcedPartyOwnerId));
    }

    rows = rows.InRange(ContractStarts, query.StartFrom, query.StartTo).InRange(ContractEnds, query.EndFrom, query.EndTo);

    // the performance guarantee is what a contractor deposits
    return (await rows.ToListAsync(cancellationToken)).Select(x => new Row(
      AgreementKind.Outsourcing, x.Id.Value, x.ContractNo.Value, x.PropertyId, x.OutsourcedPartyOwnerId, x.OutsourcingTypeId, x.ContractStatusId,
      ContractStartOf(x), ContractEndOf(x), x.ContractAmount, x.AmountFrequency?.ToString(), Annual(x.ContractAmount, x.AmountFrequency),
      x.PerformanceGuarantee, inForce.Contains(x.ContractStatusId) && x.ContractStartDate <= today, Direction: x.MoneyDirection));
  }

  private async Task<IEnumerable<Row>> AuctionsAsync(GetAgreementsQuery query, RegisterScope scope, Filter filter, CancellationToken cancellationToken)
  {
    var rows = context.Auctions.AsNoTracking();

    if (scope.Properties is { } allowed) rows = rows.Where(x => allowed.Contains(x.PropertyId));
    if (filter.Party is { } party) rows = rows.Where(x => x.SuccessfulBidderOwnerId == party);
    if (filter.TypeId is { } type) rows = rows.Where(x => x.AuctionTypeId == type);
    if (filter.StatusCodes is { } codes)
    {
      var statuses = await InForce.StatusIdsAsync<AuctionStatus>(context, codes, cancellationToken);
      rows = rows.Where(x => statuses.Contains(x.AuctionStatusId));
    }
    // an auction is a sale being run, never an agreement in force: inForce=true lists none, inForce=false all of them
    if (query.InForce == true) rows = rows.Where(x => false);
    if (scope.HasSearch)
    {
      var (upper, properties, owners) = (scope.Upper!, scope.SearchProperties, scope.SearchOwners);
      rows = rows.Where(x => ((string)(object)x.AuctionNo).Contains(upper) || properties.Contains(x.PropertyId)
        || (x.SuccessfulBidderOwnerId != null && owners.Contains(x.SuccessfulBidderOwnerId)));
    }

    rows = rows.InRange(AuctionStarts, query.StartFrom, query.StartTo).InRange(AuctionEnds, query.EndFrom, query.EndTo);

    // the amount is the winning bid once awarded, the reserve price before: a one-off sale, so its annual figure is itself
    return (await rows.ToListAsync(cancellationToken)).Select(x =>
    {
      var amount = x.WinningBidAmount ?? x.BaseReservePrice;
      return new Row(
        AgreementKind.Auction, x.Id.Value, x.AuctionNo.Value, x.PropertyId, x.SuccessfulBidderOwnerId, x.AuctionTypeId, x.AuctionStatusId,
        AuctionStartOf(x), AuctionEndOf(x), amount, null, amount, null, InForce: false, Direction: MoneyDirection.Receivable);
    });
  }

  /// The yearly equivalent of an amount: monthly x 12, annual as it is; a one-time amount (or one with no frequency) as it is.
  private static decimal? Annual(decimal? amount, AmountFrequency? frequency) => amount is null ? null : frequency switch
  {
    AmountFrequency.Monthly => amount * 12,
    _ => amount
  };

  /// Rent: monthly x 12, quarterly x 4, annual as it is.
  private static decimal Annual(decimal amount, RentFrequency frequency) => frequency switch
  {
    RentFrequency.Monthly => amount * 12,
    RentFrequency.Quarterly => amount * 4,
    _ => amount
  };

  private async Task<MasterRefs> RefsAsync(IReadOnlyCollection<Row> rows, CancellationToken cancellationToken) =>
      await masters.Refs()
        .Add<AllotmentType>(Ids(rows, AgreementKind.Allotment, r => r.TypeId)).Add<AllotmentStatus>(Ids(rows, AgreementKind.Allotment, r => r.StatusId))
        .Add<LeaseType>(Ids(rows, AgreementKind.Lease, r => r.TypeId)).Add<LeaseStatus>(Ids(rows, AgreementKind.Lease, r => r.StatusId))
        .Add<RentalType>(Ids(rows, AgreementKind.Rental, r => r.TypeId)).Add<RentalStatus>(Ids(rows, AgreementKind.Rental, r => r.StatusId))
        .Add<OutsourcingType>(Ids(rows, AgreementKind.Outsourcing, r => r.TypeId)).Add<ContractStatus>(Ids(rows, AgreementKind.Outsourcing, r => r.StatusId))
        .Add<AuctionType>(Ids(rows, AgreementKind.Auction, r => r.TypeId)).Add<AuctionStatus>(Ids(rows, AgreementKind.Auction, r => r.StatusId))
        .LoadAsync(cancellationToken);

  private static IEnumerable<MasterId?> Ids(IEnumerable<Row> rows, AgreementKind kind, Func<Row, MasterId> pick) =>
      rows.Where(r => r.Kind == kind).Select(r => (MasterId?)pick(r));
}
