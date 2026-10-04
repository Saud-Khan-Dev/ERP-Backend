/// Lists that run across all properties: every agreement, every legal matter, every transfer. Each is paged
/// (pageSize up to 5000, so a client can also export the whole filtered list).
public class RegisterEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/agreements", async (
        ISender sender, int? pageIndex, int? pageSize, string[]? kind, Guid? propertyId, Guid? townId, Guid? partyId, bool? inForce,
        DateOnly? startFrom, DateOnly? startTo, DateOnly? endFrom, DateOnly? endTo, string? search, string? sortBy, string? sortDir) =>
        (await sender.Send(new GetAgreementsQuery(
          new PaginationRequest(pageIndex ?? 0, pageSize ?? 20),
          PropertyQueryParsing.ParseEnums<AgreementKind>(kind, "kind"),
          propertyId, townId, partyId, inForce, startFrom, startTo, endFrom, endTo, search,
          PropertyQueryParsing.ParseEnum<AgreementSort>(sortBy, "sortBy") ?? AgreementSort.Ends,
          PropertyQueryParsing.ParseDescending(sortDir) ?? false))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithTags("Agreements").WithName("GetAgreements")
      .Produces<GetAgreementsQueryResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Agreements")
      .WithDescription("Allotments, leases, rentals, outsourcing contracts and auctions across all properties, in one paged list (pageSize 1-5000). "
        + "kind (repeatable): allotment | lease | rental | outsourcing | auction. partyId: the allottee / lessee / tenant / contractor / successful bidder. "
        + "inForce: true = in force (allotment ACTIVE or RESTORED, lease ACTIVE or DRAFT, rental and contract ACTIVE, auction not yet awarded or cancelled). "
        + "startFrom/startTo, endFrom/endTo filter the row's start and end dates. search matches part of the code, the party or the property. "
        + "sortBy = ends | starts | code | property | amount (default ends, soonest first, no end date last), sortDir = asc | desc.");

    app.MapGet("/legal-matters", async (
        ISender sender, int? pageIndex, int? pageSize, string[]? kind, Guid? propertyId, Guid? townId, bool? open,
        DateOnly? openedFrom, DateOnly? openedTo, DateOnly? nextFrom, DateOnly? nextTo, string? search, string? sortBy, string? sortDir) =>
        (await sender.Send(new GetLegalMattersQuery(
          new PaginationRequest(pageIndex ?? 0, pageSize ?? 20),
          PropertyQueryParsing.ParseEnums<LegalMatterKind>(kind, "kind"),
          propertyId, townId, open, openedFrom, openedTo, nextFrom, nextTo, search,
          PropertyQueryParsing.ParseEnum<LegalMatterSort>(sortBy, "sortBy") ?? LegalMatterSort.Next,
          PropertyQueryParsing.ParseDescending(sortDir) ?? false))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithTags("Legal Matters").WithName("GetLegalMatters")
      .Produces<GetLegalMattersQueryResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Legal Matters")
      .WithDescription("Encroachments, court cases, appeals and agreement violations across all properties, in one paged list (pageSize 1-5000). "
        + "kind (repeatable): encroachment | case | appeal | violation. open: true = unresolved encroachment, pending case, appeal not decided or withdrawn, "
        + "violation not rectified or closed. nextFrom/nextTo filter the next date (hearing, decision due, notice deadline). "
        + "sortBy = next | opened | code | property (default next, soonest first, no date last), sortDir = asc | desc.");

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
