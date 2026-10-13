/// Lists that run across all properties: every agreement, every legal matter, every transfer. Each is paged
/// (pageSize up to 5000, so a client can also export the whole filtered list).
public class RegisterEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/agreements", async (
        ISender sender, int? pageIndex, int? pageSize, string[]? kind, Guid? propertyId, Guid? townId, Guid? partyId, bool? inForce,
        DateOnly? startFrom, DateOnly? startTo, DateOnly? endFrom, DateOnly? endTo, string? search, string? sortBy, string? sortDir,
        string[]? status, Guid? typeId, decimal? amountMin, decimal? amountMax, int? endsWithinDays, bool? overdue) =>
        (await sender.Send(new GetAgreementsQuery(
          new PaginationRequest(pageIndex ?? 0, pageSize ?? 20),
          PropertyQueryParsing.ParseEnums<AgreementKind>(kind, "kind"),
          propertyId, townId, partyId, inForce, startFrom, startTo, endFrom, endTo, search,
          PropertyQueryParsing.ParseEnum<AgreementSort>(sortBy, "sortBy") ?? AgreementSort.Ends,
          PropertyQueryParsing.ParseDescending(sortDir) ?? false,
          PropertyQueryParsing.ParseList(status), typeId, amountMin, amountMax, endsWithinDays, overdue))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithTags("Agreements").WithName("GetAgreements")
      .Produces<GetAgreementsQueryResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Agreements")
      .WithDescription("Allotments, leases, rentals, outsourcing contracts and auctions across all properties, in one paged list (pageSize 1-5000). "
        + "kind (repeatable): allotment | lease | rental | outsourcing | auction. partyId: the allottee / lessee / tenant / contractor / successful bidder. "
        + "inForce: true = in force (allotment ACTIVE or RESTORED, lease ACTIVE, rental and contract ACTIVE, auction PLANNED, ANNOUNCED or CONDUCTED). "
        + "status (repeatable): status codes, e.g. ACTIVE, DRAFT, PLANNED. typeId: one allotment / lease / rental / outsourcing / auction type. "
        + "amountMin / amountMax: on annualAmount (monthly x 12, quarterly x 4, annual and one-off amounts as they are). "
        + "endsWithinDays: in force and ending between today and today + N days. overdue=true: in force although the end date has passed. "
        + "startFrom/startTo, endFrom/endTo filter the row's start and end dates. search matches part of the code, the party (name, code, CNIC or NTN) or the property. "
        + "sortBy = ends | starts | code | property | amount | kind | party | status (default ends, soonest first, no end date last; amount sorts on annualAmount), "
        + "sortDir = asc | desc.");

    app.MapGet("/legal-matters", async (
        ISender sender, int? pageIndex, int? pageSize, string[]? kind, Guid? propertyId, Guid? townId, bool? open,
        DateOnly? openedFrom, DateOnly? openedTo, DateOnly? nextFrom, DateOnly? nextTo, string? search, string? sortBy, string? sortDir,
        string? court, string[]? status, Guid? partyId, int? nextWithinDays, bool? hearingOverdue) =>
        (await sender.Send(new GetLegalMattersQuery(
          new PaginationRequest(pageIndex ?? 0, pageSize ?? 20),
          PropertyQueryParsing.ParseEnums<LegalMatterKind>(kind, "kind"),
          propertyId, townId, open, openedFrom, openedTo, nextFrom, nextTo, search,
          PropertyQueryParsing.ParseEnum<LegalMatterSort>(sortBy, "sortBy") ?? LegalMatterSort.Next,
          PropertyQueryParsing.ParseDescending(sortDir) ?? false,
          court, PropertyQueryParsing.ParseList(status), partyId, nextWithinDays, hearingOverdue))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithTags("Legal Matters").WithName("GetLegalMatters")
      .Produces<GetLegalMattersQueryResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Legal Matters")
      .WithDescription("Encroachments, court cases, appeals and agreement violations across all properties, in one paged list (pageSize 1-5000). "
        + "kind (repeatable): encroachment | case | appeal | violation. open: true = unresolved encroachment, pending case (status not DECIDED, APPEALED, "
        + "WITHDRAWN, SETTLED or CLOSED), appeal not decided or withdrawn, violation not rectified or closed. "
        + "court: part of the court (cases) or appellate authority (appeals). status (repeatable): the row's status code (PENDING, UnderHearing, Open ...; "
        + "case and underscores ignored). partyId: the encroacher, a case party, the appellant or the violator. "
        + "nextFrom/nextTo filter the next date (hearing, decision due, notice deadline); nextWithinDays: open with the next date between today and today + N; "
        + "hearingOverdue=true: open with a next date already past. search also matches a party's CNIC or NTN. "
        + "sortBy = next | opened | code | property | kind | title | status (default next, soonest first, no date last), sortDir = asc | desc.");

    app.MapGet("/transfers", async (
        ISender sender, int? pageIndex, int? pageSize, DateOnly? from, DateOnly? to, string? status, Guid? transferTypeId,
        Guid? propertyId, Guid? townId, string? search, string? sortBy, string? sortDir) =>
    {
      var sort = PropertyQueryParsing.ParseEnum<TransferSort>(sortBy, "sortBy") ?? TransferSort.Date;
      return (await sender.Send(new GetTransfersQuery(
        new PaginationRequest(pageIndex ?? 0, pageSize ?? 20), from, to,
        PropertyQueryParsing.ParseEnum<TransferStatus>(status, "status"), transferTypeId, propertyId, townId, search,
        sort, PropertyQueryParsing.ParseDescending(sortDir) ?? sort == TransferSort.Date))).ToOk();
    })
      .RequirePermission(PermissionCatalog.Property.View)
      .WithTags("Ownership").WithName("GetTransfers")
      .Produces<GetTransfersQueryResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Transfers")
      .WithDescription("Transfers across all properties, paged (pageSize 1-5000), with givers and receivers. from/to: transfer date. "
        + "status: initiated | approved | completed | cancelled. search matches part of the transfer no., reference no., a party or the property. "
        + "sortBy = date | code | property (default date, latest first), sortDir = asc | desc.");
  }
}
