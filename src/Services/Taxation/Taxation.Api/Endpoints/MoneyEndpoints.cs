public sealed record ReceivePaymentRequest(string Psid, decimal Amount, PaymentChannel Channel, string? TransactionRef, string? ReceiptNo, string? BankCode, DateTime? PaidAt);
public sealed record ChargeFeeRequest(FeeBasisInput Basis, Guid? PartyId, FeeSourceType? SourceType, Guid? SourceId, string? SourceNo, string? ExemptionReason,
  FeePaymentInput? PayNow);
public sealed record ReasonRequest(string Reason);
public sealed record WaiveRequest(string Reason);
public sealed record IssueClearanceRequest(Guid TaxFileId, ClearancePurpose Purpose, Guid? PropertyRefId, string? PropertyRefNo, Guid? FeeId);
public sealed record ConsumeClearanceRequest(Guid? PropertyId, ClearancePurpose? Purpose, Guid? PropertyRefId, string? PropertyRefNo);

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

    penalties.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetPenaltyRunQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("GetPenaltyRun").Produces<GetPenaltyRunQueryResult>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Surcharge Run").WithDescription("The run with every surcharge it applied (and what was paid on each) and the surcharge notices it prepared.");

    app.MapGet("/penalties", async (int? pageIndex, int? pageSize, int? fiscalYear, string? kind, Guid? circleId, Guid? runId, Guid? taxFileId, DateOnly? from, DateOnly? to,
        string? search, ISender sender) =>
        (await sender.Send(new GetPenaltiesQuery(QueryParsing.Page(pageIndex, pageSize), fiscalYear, QueryParsing.ParseEnum<PenaltyKind>(kind, "kind"), circleId, runId,
          taxFileId, from, to, search))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithTags("Penalties").WithName("GetPenalties").Produces<GetPenaltiesQueryResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Surcharges")
      .WithDescription("Every surcharge applied, newest first: kind = intra_year | year_end; from / to: the day applied; search: demand no., payer or file no. total: the sum of all that match.");

    penalties.MapPost("/preview", async (SurchargeRequest terms, ISender sender) => (await sender.Send(new PreviewSurchargeQuery(terms))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("PreviewSurcharge").Produces<SurchargePreviewDto>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Preview In-year Surcharge")
      .WithDescription("Every unpaid demand of the year it would touch, with the base, the amount and whether it can be surcharged today (the rule's period, holds, "
        + "and once a year: a demand an earlier run of the rule surcharged says so and is not surcharged again).");

    penalties.MapPost("/", async (SurchargeRequest terms, ISender sender) => (await sender.Send(new ApplySurchargeCommand(terms))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Approve)
      .WithName("ApplySurcharge").Produces<ApplySurchargeResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Apply In-year Surcharge")
      .WithDescription("targetSet = defaulters_only (still owing earlier years' dues) | all_unpaid; includesCurrent: the current year's demand is in the base "
        + "(else only what the earlier years' dues still owe); rateType percent | fixed. Each eligible demand gets the surcharge, a surcharge notice pending "
        + "confirmation and a reference re-sync. A demand already surcharged by the rule is left out.");

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
      .WithDescription("Against the property's constant reference, settled oldest year first (before the first demand, the old register's opening arrears). "
        + "channel = onelink | qr_code | online (transactionRef required; posting the same one again changes nothing) | bank_counter | cash (the manual receipt "
        + "no., issued as MR-00001 when not given; a number is used once). Not more than is behind the reference. The file is locked while it is taken.");

    payments.MapPost("/{id:guid}/reverse", async (Guid id, ReasonRequest r, ISender sender) => (await sender.Send(new ReversePaymentCommand(id, r.Reason))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Approve)
      .WithName("ReversePayment").Produces<ReversePaymentResult>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Reverse Payment")
      .WithDescription("A bounced cheque or a posting on the wrong reference: everything it paid is owed again (a year carried since is billed again on the demand "
        + "holding the balance), a fee it paid is due again, and the reference is re-synced. The payment stays on record as reversed, with the reason.");

    payments.MapGet("/", async (int? pageIndex, int? pageSize, Guid? taxFileId, string? channel, DateOnly? from, DateOnly? to, string? search, string? kind, string? status,
        Guid? receivedBy, Guid? circleId, Guid? partyId, Guid? feeId, ISender sender) =>
        (await sender.Send(new GetPaymentsQuery(QueryParsing.Page(pageIndex, pageSize), taxFileId, QueryParsing.ParseEnum<PaymentChannel>(channel, "channel"), from, to, search,
          QueryParsing.ParseEnum<PaymentKind>(kind, "kind"), QueryParsing.ParseEnum<PaymentStatus>(status, "status"), receivedBy, circleId, partyId, feeId))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("GetPayments").Produces<GetPaymentsQueryResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Payments")
      .WithDescription("from / to: dates of payment (Pakistan time). kind = demand | fee; status = posted | reversed; receivedBy: the cashier's user id (the day's "
        + "close). search: reference, transaction ref, receipt no., payer or file no.");

    payments.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetPaymentQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("GetPayment").Produces<GetPaymentQueryResult>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Payment").WithDescription("With what it settled, head by head and year by year (the receipt).");

    app.MapGet("/references/{psid}", async (string psid, ISender sender) => (await sender.Send(new GetReferenceQuery(psid))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithTags("Payments").WithName("GetReference").Produces<ReferenceDto>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Reference Inquiry").WithDescription("What is behind a 1Link reference, year by year (spaces in the reference are ignored).");

    app.MapGet("/sms-messages", async (int? pageIndex, int? pageSize, Guid? taxFileId, Guid? demandId, string? status, string? search, DateOnly? from, DateOnly? to,
        ISender sender) =>
        (await sender.Send(new GetSmsMessagesQuery(QueryParsing.Page(pageIndex, pageSize), taxFileId, demandId, QueryParsing.ParseEnum<SmsStatus>(status, "status"), search,
          from, to))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithTags("Notices").WithName("GetSmsMessages").Produces<GetSmsMessagesQueryResult>()
      .WithSummary("Get SMS")
      .WithDescription("Queued for the payers. No SMS gateway is connected yet, so they stay queued. status = queued | sent | delivered | failed; search: mobile, "
        + "text or file no.; from / to: the day queued.");

    // ---- parties, fees, clearances ----

    var parties = app.MapGroup("/parties").WithTags("Fees");

    parties.MapGet("/", async (int? pageIndex, int? pageSize, string? search, string? partyType, bool? includeInactive, ISender sender) =>
        (await sender.Send(new GetPartiesQuery(QueryParsing.Page(pageIndex, pageSize), search, QueryParsing.ParseEnum<PartyType>(partyType, "partyType"),
          includeInactive ?? false))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("GetParties").Produces<GetPartiesQueryResult>()
      .WithSummary("Get Parties")
      .WithDescription("Payers of fees who are not on a tax file: contractors, vendors, guides, applicants, staff. Active ones unless includeInactive; search: name, CNIC or mobile.");

    parties.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetPartyQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("GetParty").Produces<GetPartyQueryResult>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Party").WithDescription("With the number of fees charged to it and what is still due.");

    parties.MapPost("/", async (PartyInput party, ISender sender) => (await sender.Send(new CreatePartyCommand(party))).ToCreated(c => $"/parties/{c.Id}"))
      .RequirePermission(PermissionCatalog.Tax.Create)
      .WithName("CreateParty").Produces<CreatedResult>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Register Party").WithDescription("A CNIC / NTN is registered once.");

    parties.MapPut("/{id:guid}", async (Guid id, PartyInput party, ISender sender) => (await sender.Send(new UpdatePartyCommand(id, party))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Edit)
      .WithName("UpdateParty").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Update Party");

    foreach (var (route, active) in new[] { ("activate", true), ("deactivate", false) })
    {
      parties.MapPost($"/{{id:guid}}/{route}", async (Guid id, ISender sender) => (await sender.Send(new SetPartyActivationCommand(id, active))).ToOk())
        .RequirePermission(PermissionCatalog.Tax.Edit)
        .WithName(active ? "ActivateParty" : "DeactivateParty").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary(active ? "Activate Party" : "Deactivate Party").WithDescription(active ? "" : "Refused while the party has fees due.");
    }

    parties.MapDelete("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new DeletePartyCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Delete)
      .WithName("DeleteParty").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Delete Party").WithDescription("A party registered by mistake: no fee was ever charged to it.");

    var fees = app.MapGroup("/fees").WithTags("Fees");

    fees.MapPost("/quote", async (FeeBasisInput basis, ISender sender) => (await sender.Send(new QuoteFeeQuery(basis))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("QuoteFee").Produces<FeeQuoteDto>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Quote Fee")
      .WithDescription("The fee of a head code from the rate set in force: quantity (kanal, sq ft, marla, pages ...; rounded up when the rate says so), value (for a "
        + "percentage), extensionMonths (non-utilization of a plan extension). A tax file picks its town's and category's own rates.");

    fees.MapPost("/", async (ChargeFeeRequest r, ISender sender) =>
        (await sender.Send(new ChargeFeeCommand(r.Basis, r.PartyId, r.SourceType, r.SourceId, r.SourceNo, r.ExemptionReason, r.PayNow))).ToCreated(c => $"/fees/{c.Id}"))
      .RequirePermission(PermissionCatalog.Tax.Create)
      .WithName("ChargeFee").Produces<ChargeFeeResult>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Charge Fee")
      .WithDescription("Recorded due. payNow {channel, receiptNo, transactionRef, bankCode, paidAt}: its receipt is taken at once (needs TAX.POST); an exemption "
        + "reason waives it at once (needs TAX.APPROVE; a gift or inheritance transfer). A paid water connection fee starts the file's water charges from today.");

    fees.MapGet("/", async (int? pageIndex, int? pageSize, Guid? taxFileId, Guid? partyId, string? headCode, string? status, string? search, DateOnly? from, DateOnly? to,
        ISender sender) =>
        (await sender.Send(new GetFeesQuery(QueryParsing.Page(pageIndex, pageSize), taxFileId, partyId, headCode, QueryParsing.ParseEnum<FeeStatus>(status, "status"),
          search, from, to))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("GetFees").Produces<GetFeesQueryResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Fees").WithDescription("search: fee no., source no., file no., property code or party; from / to: the day charged.");

    fees.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetFeeQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("GetFee").Produces<GetFeeQueryResult>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Fee").WithDescription("With its receipt when paid.");

    fees.MapPost("/{id:guid}/pay", async (Guid id, FeePaymentInput payment, ISender sender) => (await sender.Send(new PayFeeCommand(id, payment))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Post)
      .WithName("PayFee").Produces<PayFeeResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Take Fee Receipt")
      .WithDescription("A payment for the fee's whole amount: channel cash | bank_counter (receiptNo, issued when not given) | onelink | qr_code | online (transactionRef).");

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
      .WithDescription("purpose = transfer | building_plan. Only when nothing is owed on the file: nothing behind its reference and no fee due on it. Valid for "
        + "CLEARANCE_VALID_DAYS days counting the day of issue.");

    clearances.MapGet("/", async (Guid? taxFileId, Guid? propertyId, string? status, string? search, DateOnly? from, DateOnly? to, int? expiringWithinDays, int? pageIndex,
        int? pageSize, ISender sender) =>
        (await sender.Send(new GetClearancesQuery(taxFileId, QueryParsing.ParseEnum<ClearanceStatus>(status, "status"), propertyId, search, from, to, expiringWithinDays,
          pageIndex is null && pageSize is null ? null : QueryParsing.Page(pageIndex, pageSize)))).ToOk())
      .RequireAnyPermission(TaxAuthorization.ClearanceReader)
      .WithName("GetClearances").Produces<GetClearancesQueryResult>()
      .WithSummary("Get Clearances")
      .WithDescription("Newest first; count = every clearance that matches. status = issued (valid today) | expired | consumed | revoked, worked out for today. "
        + "propertyId: the certificates of a Property property. search: clearance no., transfer or plan no., file no. or property code. from / to: issue dates. "
        + "expiringWithinDays: valid ones whose last day is that close. Without pageIndex / pageSize the newest 500.");

    clearances.MapGet("/by-number/{clearanceNo}", async (string clearanceNo, ISender sender) => (await sender.Send(new GetClearanceByNumberQuery(clearanceNo))).ToOk())
      .RequireAnyPermission(TaxAuthorization.ClearanceReader)
      .WithName("GetClearanceByNumber").Produces<GetClearanceQueryResult>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Find a Clearance by its Number")
      .WithDescription("CLR-00012 as printed on the certificate - what Property's transfer and plan forms check before they approve. TAX.VIEW or PROPERTY.APPROVE.");

    clearances.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetClearanceQuery(id))).ToOk())
      .RequireAnyPermission(TaxAuthorization.ClearanceReader)
      .WithName("GetClearance").Produces<GetClearanceQueryResult>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Clearance").WithDescription("One clearance certificate, for printing.");

    clearances.MapPost("/{id:guid}/consume", async (Guid id, ConsumeClearanceRequest? r, ISender sender) =>
        (await sender.Send(new ConsumeClearanceCommand(id, r?.PropertyId, r?.Purpose, r?.PropertyRefId, r?.PropertyRefNo))).ToOk())
      .RequireAnyPermission(TaxAuthorization.ClearanceConsumer)
      .WithName("ConsumeClearance").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Use a Clearance")
      .WithDescription("When Property approves the transfer or plan with it (TAX.APPROVE or PROPERTY.APPROVE). Refused, with the reason, when it was already used, "
        + "has expired or was revoked; propertyId and purpose, when sent, must be the certificate's. propertyRefId / propertyRefNo: the transfer or plan that used it.");

    clearances.MapPost("/{id:guid}/revoke", async (Guid id, ISender sender) => (await sender.Send(new RevokeClearanceCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Approve)
      .WithName("RevokeClearance").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Revoke Clearance");

  }
}
