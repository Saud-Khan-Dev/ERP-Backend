public sealed record DismissGatewayPaymentRequest(string Reason);
public sealed record ImportSettlementRequest(PaymentChannel Channel, string BatchRef, DateOnly SettlementDate, DateOnly CoversFrom, DateOnly CoversTo, IReadOnlyList<SettlementLineInput> Lines);

/// For the Section: the payments gateways reported and the bank's settlement reports. The route a gateway itself calls
/// belongs to its adapter and does not exist yet, so nothing here accepts a payment from outside.
public class GatewayEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    var gateway = app.MapGroup("/gateway-payments").WithTags("Gateway payments");

    gateway.MapGet("/", async (int? pageIndex, int? pageSize, string? status, string? search, bool? waiting, ISender sender) =>
        (await sender.Send(new GetGatewayNotificationsQuery(QueryParsing.Page(pageIndex, pageSize), QueryParsing.ParseEnum<GatewayNotificationStatus>(status, "status"), search, waiting ?? false))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("GetGatewayPayments").Produces<GetGatewayNotificationsQueryResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Gateway Payments").WithDescription("What gateways reported, newest first; status=held lists what is held, with why; waiting=true what the bell counts (held, or left half worked). Needs every town.");

    gateway.MapPost("/{id:guid}/retry", async (Guid id, ISender sender) => (await sender.Send(new RetryGatewayNotificationCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Post)
      .WithName("RetryGatewayPayment").Produces<RetryGatewayNotificationResult>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Retry Gateway Payment")
      .WithDescription("Applies a held payment again after what stopped it was fixed, with the facts on record: nothing about it can be changed here. Needs every town.");

    gateway.MapPost("/{id:guid}/dismiss", async (Guid id, DismissGatewayPaymentRequest r, ISender sender) => (await sender.Send(new DismissGatewayNotificationCommand(id, r.Reason))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Reverse)
      .WithName("DismissGatewayPayment").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Dismiss Gateway Payment").WithDescription("Closes a held payment with a reason (the money was refunded by the bank, for example). No payment is posted. Needs every town.");

    var settlements = app.MapGroup("/settlements").WithTags("Settlements");

    settlements.MapPost("/", async (ImportSettlementRequest r, ISender sender) =>
        (await sender.Send(new ImportSettlementCommand(r.Channel, r.BatchRef, r.SettlementDate, r.CoversFrom, r.CoversTo, r.Lines))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Reconcile)
      .WithName("ImportSettlement").Produces<SettlementBatchDto>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict)
      .WithSummary("Import Settlement Report")
      .WithDescription("Compares the bank's report with the payments on record, line by line. It changes no payment. A report is imported once. Needs every town.");

    settlements.MapGet("/", async (int? pageIndex, int? pageSize, ISender sender) => (await sender.Send(new GetSettlementsQuery(QueryParsing.Page(pageIndex, pageSize)))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Reconcile)
      .WithName("GetSettlements").Produces<GetSettlementsQueryResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Settlement Reports").WithDescription("Newest first. Needs every town.");

    settlements.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetSettlementQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Reconcile)
      .WithName("GetSettlement").Produces<GetSettlementQueryResult>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Settlement Report").WithDescription("Its lines, differences first, and the payments of its period no report has settled yet. Needs every town.");
  }
}
