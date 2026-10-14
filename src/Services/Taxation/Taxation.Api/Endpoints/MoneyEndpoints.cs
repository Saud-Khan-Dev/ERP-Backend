public sealed record ReceivePaymentRequest(string Psid, decimal Amount, PaymentChannel Channel, string? TransactionRef, string? ReceiptNo, string? BankCode, DateTime? PaidAt);
public sealed record ChargeFeeRequest(FeeBasisInput Basis, Guid? PartyId, FeeSourceType? SourceType, Guid? SourceId, string? SourceNo, string? ExemptionReason, bool Paid);
public sealed record WaiveRequest(string Reason);
public sealed record IssueClearanceRequest(Guid TaxFileId, ClearancePurpose Purpose, Guid? PropertyRefId, string? PropertyRefNo, Guid? FeeId);

/// Surcharges, payments against the constant reference, SMS, fees, clearances and the collection summary.
public class MoneyEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    // ---- penalties ----

    var penalties = app.MapGroup("/penalty-runs").WithTags("Penalties");

    penalties.MapGet("/", async (int? fiscalYear, ISender sender) => (await sender.Send(new GetPenaltyRunsQuery(fiscalYear))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("GetPenaltyRuns").Produces<GetPenaltyRunsQueryResult>()
      .WithSummary("Get Surcharge Runs");

    penalties.MapPost("/preview", async (SurchargeRequest terms, ISender sender) => (await sender.Send(new PreviewSurchargeQuery(terms))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("PreviewSurcharge").Produces<SurchargePreviewDto>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Preview In-year Surcharge")
      .WithDescription("Every unpaid demand of the year it would touch, with the base, the amount and whether it can be surcharged today (the rule's period, holds).");

    penalties.MapPost("/", async (SurchargeRequest terms, ISender sender) => (await sender.Send(new ApplySurchargeCommand(terms))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Approve)
      .WithName("ApplySurcharge").Produces<ApplySurchargeResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Apply In-year Surcharge")
      .WithDescription("targetSet = defaulters_only (carrying arrears) | all_unpaid; includesCurrent: the current year's demand is in the base; rateType percent | fixed. "
        + "Each eligible demand gets the surcharge, a surcharge notice pending confirmation and a reference re-sync.");

    app.MapPost("/penalties/year-end", async (ISender sender) => (await sender.Send(new ApplyYearEndCommand())).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Edit)
      .WithTags("Penalties").WithName("ApplyYearEnd").Produces<ApplyYearEndResult>()
      .WithSummary("Apply Year-end Surcharge")
      .WithDescription("The automatic surcharge on whatever is outstanding once 30 June of a demand's year is over; the demand becomes defaulted. Each demand takes it "
        + "once, so it is safe to run daily (a demand run also applies it first).");

    // ---- payments ----

    var payments = app.MapGroup("/payments").WithTags("Payments");

    payments.MapPost("/", async (ReceivePaymentRequest r, ISender sender) =>
        (await sender.Send(new ReceivePaymentCommand(r.Psid, r.Amount, r.Channel, r.TransactionRef, r.ReceiptNo, r.BankCode, r.PaidAt))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Post)
      .WithName("ReceivePayment").Produces<ReceivePaymentResult>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Receive Payment")
      .WithDescription("Against the property's constant reference, settled oldest year first. channel = onelink | qr_code | online (transactionRef required; posting the "
        + "same one again changes nothing) | bank_counter | cash (the manual receipt no., issued as MR-00001 when not given). Not more than is behind the reference.");

    payments.MapGet("/", async (int? pageIndex, int? pageSize, Guid? taxFileId, string? channel, DateOnly? from, DateOnly? to, string? search, ISender sender) =>
        (await sender.Send(new GetPaymentsQuery(QueryParsing.Page(pageIndex, pageSize), taxFileId, QueryParsing.ParseEnum<PaymentChannel>(channel, "channel"), from, to, search))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("GetPayments").Produces<GetPaymentsQueryResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Payments").WithDescription("from / to: dates of payment (Pakistan time).");

    payments.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetPaymentQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("GetPayment").Produces<GetPaymentQueryResult>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Payment").WithDescription("With what it settled, head by head.");

    app.MapGet("/references/{psid}", async (string psid, ISender sender) => (await sender.Send(new GetReferenceQuery(psid))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithTags("Payments").WithName("GetReference").Produces<ReferenceDto>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Reference Inquiry").WithDescription("What is behind a 1Link reference, year by year (spaces in the reference are ignored).");

    app.MapGet("/sms-messages", async (int? pageIndex, int? pageSize, Guid? taxFileId, Guid? demandId, ISender sender) =>
        (await sender.Send(new GetSmsMessagesQuery(QueryParsing.Page(pageIndex, pageSize), taxFileId, demandId))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithTags("Notices").WithName("GetSmsMessages").Produces<GetSmsMessagesQueryResult>()
      .WithSummary("Get SMS").WithDescription("Queued for the payers. No SMS gateway is connected yet, so they stay queued.");

    // ---- parties, fees, clearances ----

    var parties = app.MapGroup("/parties").WithTags("Fees");

    parties.MapGet("/", async (int? pageIndex, int? pageSize, string? search, string? partyType, ISender sender) =>
        (await sender.Send(new GetPartiesQuery(QueryParsing.Page(pageIndex, pageSize), search, QueryParsing.ParseEnum<PartyType>(partyType, "partyType")))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("GetParties").Produces<GetPartiesQueryResult>()
      .WithSummary("Get Parties").WithDescription("Payers of fees who are not on a tax file: contractors, vendors, guides, applicants, staff.");

    parties.MapPost("/", async (PartyInput party, ISender sender) => (await sender.Send(new CreatePartyCommand(party))).ToCreated(c => $"/parties/{c.Id}"))
      .RequirePermission(PermissionCatalog.Tax.Create)
      .WithName("CreateParty").Produces<CreatedResult>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Create Party");

    parties.MapPut("/{id:guid}", async (Guid id, PartyInput party, ISender sender) => (await sender.Send(new UpdatePartyCommand(id, party))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Edit)
      .WithName("UpdateParty").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Update Party");

    var fees = app.MapGroup("/fees").WithTags("Fees");

    fees.MapPost("/quote", async (FeeBasisInput basis, ISender sender) => (await sender.Send(new QuoteFeeQuery(basis))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("QuoteFee").Produces<FeeQuoteDto>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Quote Fee")
      .WithDescription("The fee of a head code from the rate set in force: quantity (kanal, sq ft, marla, pages ...), value (for a percentage), extensionMonths "
        + "(non-utilization of a plan extension). A tax file picks its town's and category's own rates.");

    fees.MapPost("/", async (ChargeFeeRequest r, ISender sender) =>
        (await sender.Send(new ChargeFeeCommand(r.Basis, r.PartyId, r.SourceType, r.SourceId, r.SourceNo, r.ExemptionReason, r.Paid))).ToCreated(c => $"/fees/{c.Id}"))
      .RequirePermission(PermissionCatalog.Tax.Create)
      .WithName("ChargeFee").Produces<CreatedResult>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Charge Fee")
      .WithDescription("Recorded due, or paid when received at once; an exemption reason waives it (gift / inheritance transfer). A paid water connection fee starts "
        + "the file's water charges from today.");

    fees.MapGet("/", async (int? pageIndex, int? pageSize, Guid? taxFileId, Guid? partyId, string? headCode, string? status, ISender sender) =>
        (await sender.Send(new GetFeesQuery(QueryParsing.Page(pageIndex, pageSize), taxFileId, partyId, headCode, QueryParsing.ParseEnum<FeeStatus>(status, "status")))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("GetFees").Produces<GetFeesQueryResult>()
      .WithSummary("Get Fees");

    fees.MapPost("/{id:guid}/paid", async (Guid id, ISender sender) => (await sender.Send(new MarkFeePaidCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Post)
      .WithName("MarkFeePaid").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Mark Fee Paid");

    fees.MapPost("/{id:guid}/waive", async (Guid id, WaiveRequest r, ISender sender) => (await sender.Send(new WaiveFeeCommand(id, r.Reason))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Approve)
      .WithName("WaiveFee").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Waive Fee");

    var clearances = app.MapGroup("/clearances").WithTags("Fees");

    clearances.MapPost("/", async (IssueClearanceRequest r, ISender sender) =>
        (await sender.Send(new IssueClearanceCommand(r.TaxFileId, r.Purpose, r.PropertyRefId, r.PropertyRefNo, r.FeeId))).ToCreated(c => $"/clearances/{c.Id}"))
      .RequirePermission(PermissionCatalog.Tax.Approve)
      .WithName("IssueClearance").Produces<CreatedResult>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Issue Clearance")
      .WithDescription("purpose = transfer | building_plan. Only when nothing is behind the file's reference; valid for CLEARANCE_VALID_DAYS.");

    clearances.MapGet("/", async (Guid? taxFileId, string? status, ISender sender) =>
        (await sender.Send(new GetClearancesQuery(taxFileId, QueryParsing.ParseEnum<ClearanceStatus>(status, "status")))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("GetClearances").Produces<GetClearancesQueryResult>()
      .WithSummary("Get Clearances").WithDescription("Newest 500; an issued clearance past its date shows as expired.");

    clearances.MapPost("/{id:guid}/consume", async (Guid id, ISender sender) => (await sender.Send(new ConsumeClearanceCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Approve)
      .WithName("ConsumeClearance").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Mark Clearance Used").WithDescription("When Property approves the transfer or plan with it.");

    clearances.MapPost("/{id:guid}/revoke", async (Guid id, ISender sender) => (await sender.Send(new RevokeClearanceCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Approve)
      .WithName("RevokeClearance").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Revoke Clearance");

    // ---- reports ----

    app.MapGet("/tax-reports/collection", async (int fiscalYear, ISender sender) => (await sender.Send(new GetCollectionSummaryQuery(fiscalYear))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithTags("Reports").WithName("GetCollectionSummary").Produces<CollectionSummaryDto>()
      .WithSummary("Collection Summary")
      .WithDescription("The year: demanded (this year's charges, arrears billed, surcharges), recovered and the recovery %, by circle, what is outstanding over all years, "
        + "the surcharges applied and the notices awaiting confirmation.");
  }
}
