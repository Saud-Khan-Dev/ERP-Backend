/// The taxpayer portal (/my): for an account linked to a Property owner (Identity `PUT /users/{id}/owner-link`). Any
/// signed-in person may call it; each sees only the files they pay on today (TaxpayerScope), and nothing can be changed.
public class TaxpayerEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    var my = app.MapGroup("/my").WithTags("Taxpayer portal").RequireSignedIn();

    my.MapGet("/", async (ISender sender) => (await sender.Send(new GetMyTaxesQuery())).ToOk())
      .WithName("GetMyTaxes").Produces<MyTaxesDto>()
      .WithSummary("My Taxes")
      .WithDescription("The person's properties with what is due on each, the current year's demand and the total. linked=false: the account is not a taxpayer's.");

    my.MapGet("/tax-files/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetMyTaxFileQuery(id))).ToOk())
      .WithName("GetMyTaxFile").Produces<MyTaxFileDetailDto>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("My Tax File").WithDescription("One property: its demands year by year, the notices issued, the payments.");

    my.MapGet("/tax-files/{id:guid}/payment", async (Guid id, ISender sender) => (await sender.Send(new GetMyPaymentDetailsQuery(id))).ToOk())
      .WithName("GetMyPaymentDetails").Produces<MyPaymentDetailsDto>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("How To Pay")
      .WithDescription("The 1Link reference to type in a banking app's bill payment, and the payment QR (EMVCo) for the whole amount due - null when nothing is due. "
        + "The money is paid in the bank's app; it reaches the file when the bank reports it.");

    my.MapGet("/demands/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetMyDemandQuery(id))).ToOk())
      .WithName("GetMyDemand").Produces<GetDemandQueryResult>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("My Demand").WithDescription("A year's demand head by head, with its arrears, surcharges and the notices issued on it.");

    my.MapGet("/notices/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetMyNoticeQuery(id))).ToOk())
      .WithName("GetMyNotice").Produces<GetNoticeQueryResult>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("My Notice").WithDescription("An issued notice exactly as printed (letterhead, particulars, payment block). Logos: GET /letterheads/{id}/logo/{side}.");

    my.MapGet("/messages", async (int? pageIndex, int? pageSize, ISender sender) => (await sender.Send(new GetMyMessagesQuery(QueryParsing.Page(pageIndex, pageSize)))).ToOk())
      .WithName("GetMyMessages").Produces<GetMyMessagesQueryResult>()
      .WithSummary("My Messages").WithDescription("The SMS the Section sent the person, newest first.");
  }
}
