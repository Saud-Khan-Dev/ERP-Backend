// Request bodies that wrap more than one command argument.
public sealed record AllotPropertyRequest(Guid AllotteeOwnerId, AllotmentDetailsInput Allotment);
public sealed record CancelAllotmentRequest(DateOnly CancellationDate, string Reason, string? OrderRef = null);
public sealed record RestoreAllotmentRequest(DateOnly RestorationDate, string? OrderRef = null);
public sealed record ChangeMasterStatusRequest(Guid StatusId);
public sealed record ConfirmOwnershipRequest(DateOnly EffectiveFrom, decimal OwnershipSharePct = 100, Guid? TenureTypeId = null, string? ReferenceNo = null, string? Remarks = null);

public sealed record CreateLeaseRequest(Guid LesseeOwnerId, LeaseTermsInput Terms, bool AsDraft = false);
public sealed record RenewLeaseRequest(LeaseTermsInput Terms, Guid? LesseeOwnerId = null);
public sealed record EndAgreementRequest(DateOnly TerminationDate, string Reason);

public sealed record CreateRentalRequest(Guid TenantOwnerId, RentalTermsInput Terms);
public sealed record RenewRentalRequest(RentalTermsInput Terms, Guid? TenantOwnerId = null);

public sealed record ViolationNoticeRequest(string NoticeNo, DateOnly NoticeDate, DateOnly? NoticeDeadline = null);
public sealed record ImposeFineRequest(decimal FineAmount);
public sealed record FineStatusRequest(FineStatus FineStatus);

public sealed record RecordBidRequest(Guid BidderOwnerId, decimal BidAmount, decimal? EarnestMoney = null, string? Remarks = null);
public sealed record AwardAuctionRequest(DateOnly AwardDate, Guid? WinningBidId = null, Guid? SuccessfulBidderOwnerId = null, decimal? WinningBidAmount = null, string? AwardReferenceNo = null);

public sealed record CreateOutsourcingRequest(Guid OutsourcedPartyOwnerId, OutsourcingTermsInput Terms);

/// Management records (schema guide, "Management records"): allotment, lease, rental, agreement
/// violations (Act s.28-A), auction and outsourcing. Each has its own dates, amounts and status life-cycle.
public class ManagementEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    MapAllotments(app);
    MapLeases(app);
    MapRentals(app);
    MapViolations(app);
    MapAuctions(app);
    MapOutsourcing(app);
  }

  // =====================================================
  // ALLOTMENT
  // =====================================================

  private static void MapAllotments(IEndpointRouteBuilder app)
  {
    app.MapPost("/properties/{id:guid}/allotments", async (Guid id, AllotPropertyRequest request, ISender sender) =>
        (await sender.Send(new AllotPropertyCommand(id, request.AllotteeOwnerId, request.Allotment))).ToCreated(r => $"/allotments/{r.Id}"))
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithTags("Allotments").WithName("AllotProperty")
      .Produces<AllotPropertyCommandResult>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Allot Property")
      .WithDescription("Allots the plot (ALT-00001 is generated; status ACTIVE). Allotment is not ownership — confirm it separately.");

    app.MapGet("/properties/{id:guid}/allotments", async (Guid id, bool? includeInactive, ISender sender) =>
        (await sender.Send(new GetPropertyAllotmentsQuery(id, includeInactive ?? false))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithTags("Allotments").WithName("GetPropertyAllotments")
      .Produces<GetAllotmentsQueryResult>().WithSummary("Get Allotments");

    var allotments = app.MapGroup("/allotments").WithTags("Allotments");

    allotments.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetAllotmentQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetAllotment").Produces<GetAllotmentQueryResult>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Allotment");

    allotments.MapPut("/{id:guid}", async (Guid id, AllotmentDetailsInput allotment, ISender sender) =>
        (await sender.Send(new UpdateAllotmentCommand(id, allotment))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("UpdateAllotment").Produces<UpdateAllotmentCommandResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Update Allotment").WithDescription("Corrects the allotment's details; status changes use cancel / restore / status.");

    allotments.MapPost("/{id:guid}/cancel", async (Guid id, CancelAllotmentRequest request, ISender sender) =>
        (await sender.Send(new CancelAllotmentCommand(id, request.CancellationDate, request.Reason, request.OrderRef))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Approve)
      .WithName("CancelAllotment").Produces<AllotmentLifecycleResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Cancel Allotment").WithDescription("Act s.6(4)(c). An appeal against the cancellation is filed under /properties/{id}/appeals.");

    allotments.MapPost("/{id:guid}/restore", async (Guid id, RestoreAllotmentRequest request, ISender sender) =>
        (await sender.Send(new RestoreAllotmentCommand(id, request.RestorationDate, request.OrderRef))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Approve)
      .WithName("RestoreAllotment").Produces<AllotmentLifecycleResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Restore Allotment").WithDescription("Reverses a cancellation; the cancellation details stay on the record.");

    allotments.MapPost("/{id:guid}/status", async (Guid id, ChangeMasterStatusRequest request, ISender sender) =>
        (await sender.Send(new ChangeAllotmentStatusCommand(id, request.StatusId))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("ChangeAllotmentStatus").Produces<AllotmentLifecycleResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Change Allotment Status").WithDescription("Any other admin-defined status (Surrendered, Expired ...).");

    allotments.MapPost("/{id:guid}/deactivate", async (Guid id, ISender sender) =>
        (await sender.Send(new DeactivateAllotmentCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Delete)
      .WithName("DeactivateAllotment").Produces<AllotmentLifecycleResult>()
      .WithSummary("Deactivate Allotment").WithDescription("Withdraws an allotment entered in error; nothing is hard-deleted.");

    allotments.MapPost("/{id:guid}/confirm-ownership", async (Guid id, ConfirmOwnershipRequest request, ISender sender) =>
        (await sender.Send(new ConfirmAllotmentOwnershipCommand(id, request.EffectiveFrom, request.OwnershipSharePct, request.TenureTypeId, request.ReferenceNo, request.Remarks))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Approve)
      .WithName("ConfirmAllotmentOwnership").Produces<ConfirmAllotmentOwnershipCommandResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Confirm Allottee As Owner")
      .WithDescription("GDA confirms the allottee as legal owner: a property_ownership row with acquired_via_allotment_id. Shares may not exceed 100%.");
  }

  // =====================================================
  // LEASE
  // =====================================================

  private static void MapLeases(IEndpointRouteBuilder app)
  {
    app.MapPost("/properties/{id:guid}/leases", async (Guid id, CreateLeaseRequest request, ISender sender) =>
        (await sender.Send(new CreateLeaseCommand(id, request.LesseeOwnerId, request.Terms, request.AsDraft))).ToCreated(r => $"/leases/{r.Id}"))
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithTags("Leases").WithName("CreateLease")
      .Produces<CreateLeaseCommandResult>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Create Lease").WithDescription("LSE-00001 is generated. asDraft=true keeps the terms editable until the lease is activated.");

    app.MapGet("/properties/{id:guid}/leases", async (Guid id, ISender sender) => (await sender.Send(new GetPropertyLeasesQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithTags("Leases").WithName("GetPropertyLeases").Produces<GetLeasesQueryResult>().WithSummary("Get Leases");

    var leases = app.MapGroup("/leases").WithTags("Leases");

    leases.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetLeaseQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetLease").Produces<GetLeaseQueryResult>().ProducesProblem(StatusCodes.Status404NotFound).WithSummary("Get Lease");

    leases.MapPut("/{id:guid}", async (Guid id, LeaseTermsInput terms, ISender sender) =>
        (await sender.Send(new UpdateLeaseTermsCommand(id, terms))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("UpdateLeaseTerms").Produces<UpdateLeaseTermsCommandResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Update Draft Lease").WithDescription("Draft leases only. Once in force the terms are history (rule 2) — renew to change them.");

    leases.MapPost("/{id:guid}/activate", async (Guid id, ISender sender) => (await sender.Send(new ActivateLeaseCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("ActivateLease").Produces<LeaseLifecycleResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Activate Lease");

    leases.MapPost("/{id:guid}/renew", async (Guid id, RenewLeaseRequest request, ISender sender) =>
        (await sender.Send(new RenewLeaseCommand(id, request.Terms, request.LesseeOwnerId))).ToCreated(r => $"/leases/{r.Id}"))
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("RenewLease").Produces<RenewLeaseCommandResult>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Renew Lease").WithDescription("Creates the renewing lease (renewed_from_lease_id → this one); this lease becomes RENEWED.");

    leases.MapPost("/{id:guid}/terminate", async (Guid id, EndAgreementRequest request, ISender sender) =>
        (await sender.Send(new TerminateLeaseCommand(id, request.TerminationDate, request.Reason))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Approve)
      .WithName("TerminateLease").Produces<LeaseLifecycleResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Terminate Lease");

    leases.MapPost("/{id:guid}/expire", async (Guid id, ISender sender) => (await sender.Send(new ExpireLeaseCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("ExpireLease").Produces<LeaseLifecycleResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Mark Lease Expired");
  }

  // =====================================================
  // RENTAL
  // =====================================================

  private static void MapRentals(IEndpointRouteBuilder app)
  {
    app.MapPost("/properties/{id:guid}/rentals", async (Guid id, CreateRentalRequest request, ISender sender) =>
        (await sender.Send(new CreateRentalCommand(id, request.TenantOwnerId, request.Terms))).ToCreated(r => $"/rentals/{r.Id}"))
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithTags("Rentals").WithName("CreateRental")
      .Produces<CreateRentalCommandResult>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Create Rental").WithDescription("RNT-00001 is generated; the rental starts ACTIVE. Rent collection belongs to the Tax / Finance Module.");

    app.MapGet("/properties/{id:guid}/rentals", async (Guid id, ISender sender) => (await sender.Send(new GetPropertyRentalsQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithTags("Rentals").WithName("GetPropertyRentals").Produces<GetRentalsQueryResult>().WithSummary("Get Rentals");

    var rentals = app.MapGroup("/rentals").WithTags("Rentals");

    rentals.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetRentalQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetRental").Produces<GetRentalQueryResult>().ProducesProblem(StatusCodes.Status404NotFound).WithSummary("Get Rental");

    rentals.MapPut("/{id:guid}", async (Guid id, RentalTermsInput terms, ISender sender) =>
        (await sender.Send(new UpdateRentalTermsCommand(id, terms))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("UpdateRentalTerms").Produces<UpdateRentalTermsCommandResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Update Rental");

    rentals.MapPost("/{id:guid}/renew", async (Guid id, RenewRentalRequest request, ISender sender) =>
        (await sender.Send(new RenewRentalCommand(id, request.Terms, request.TenantOwnerId))).ToCreated(r => $"/rentals/{r.Id}"))
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("RenewRental").Produces<RenewRentalCommandResult>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Renew Rental").WithDescription("A new rental row (renewed_from_rental_id → this one); this one ends.");

    rentals.MapPost("/{id:guid}/terminate", async (Guid id, EndAgreementRequest request, ISender sender) =>
        (await sender.Send(new TerminateRentalCommand(id, request.TerminationDate, request.Reason))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("TerminateRental").Produces<RentalLifecycleResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Terminate Rental");
  }

  // =====================================================
  // AGREEMENT VIOLATIONS (Act s.28-A)
  // =====================================================

  private static void MapViolations(IEndpointRouteBuilder app)
  {
    app.MapPost("/properties/{id:guid}/violations", async (Guid id, ViolationInput violation, ISender sender) =>
        (await sender.Send(new RecordViolationCommand(id, violation))).ToCreated(r => $"/violations/{r.Id}"))
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithTags("Agreement Violations").WithName("RecordViolation")
      .Produces<RecordViolationCommandResult>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Record Agreement Violation")
      .WithDescription("Exactly one of leaseId / rentalId / transferId (rule 4). The third violation of the same agreement within the last notice's deadline cancels the lease or rental (rule 5, Act s.28-A).");

    app.MapGet("/properties/{id:guid}/violations", async (Guid id, Guid? leaseId, Guid? rentalId, Guid? transferId, ISender sender) =>
        (await sender.Send(new GetPropertyViolationsQuery(id, leaseId, rentalId, transferId))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithTags("Agreement Violations").WithName("GetPropertyViolations").Produces<GetViolationsQueryResult>()
      .WithSummary("Get Agreement Violations");

    var violations = app.MapGroup("/violations").WithTags("Agreement Violations");

    violations.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetViolationQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetViolation").Produces<GetViolationQueryResult>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Agreement Violation");

    violations.MapPost("/{id:guid}/notice", async (Guid id, ViolationNoticeRequest request, ISender sender) =>
        (await sender.Send(new IssueViolationNoticeCommand(id, request.NoticeNo, request.NoticeDate, request.NoticeDeadline))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("IssueViolationNotice").Produces<ViolationActionResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Issue Notice").WithDescription("The notice deadline is the period rule 5 counts repeat violations against.");

    violations.MapPost("/{id:guid}/fine", async (Guid id, ImposeFineRequest request, ISender sender) =>
        (await sender.Send(new ImposeFineCommand(id, request.FineAmount))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Approve)
      .WithName("ImposeFine").Produces<ViolationActionResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Impose Fine").WithDescription("Up to Rs 1,000,000 (s.28-A), recorded against the signed-in officer. Collection is the Tax / Finance Module's job.");

    violations.MapPost("/{id:guid}/fine-status", async (Guid id, FineStatusRequest request, ISender sender) =>
        (await sender.Send(new SetFineStatusCommand(id, request.FineStatus))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("SetFineStatus").Produces<ViolationActionResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Update Fine Status").WithDescription("Paid, Waived, or RecoveryAsArrears (s.28(2)).");

    violations.MapPost("/{id:guid}/rectify", async (Guid id, RemarksRequest request, ISender sender) =>
        (await sender.Send(new RectifyViolationCommand(id, request.Remarks))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("RectifyViolation").Produces<ViolationActionResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Mark Violation Rectified");
  }

  // =====================================================
  // AUCTION
  // =====================================================

  private static void MapAuctions(IEndpointRouteBuilder app)
  {
    app.MapPost("/properties/{id:guid}/auctions", async (Guid id, AuctionDetailsInput auction, ISender sender) =>
        (await sender.Send(new PlanAuctionCommand(id, auction))).ToCreated(r => $"/auctions/{r.Id}"))
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithTags("Auctions").WithName("PlanAuction")
      .Produces<PlanAuctionCommandResult>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Plan Auction").WithDescription("AUC-00001 is generated; the auction starts PLANNED.");

    app.MapGet("/properties/{id:guid}/auctions", async (Guid id, ISender sender) => (await sender.Send(new GetPropertyAuctionsQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithTags("Auctions").WithName("GetPropertyAuctions").Produces<GetAuctionsQueryResult>().WithSummary("Get Auctions");

    var auctions = app.MapGroup("/auctions").WithTags("Auctions");

    auctions.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetAuctionQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetAuction").Produces<GetAuctionQueryResult>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Auction").WithDescription("The auction with its bids, ranked highest first.");

    auctions.MapPut("/{id:guid}", async (Guid id, AuctionDetailsInput auction, ISender sender) =>
        (await sender.Send(new UpdateAuctionCommand(id, auction))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("UpdateAuction").Produces<AuctionActionResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Update Auction");

    auctions.MapPost("/{id:guid}/status", async (Guid id, ChangeMasterStatusRequest request, ISender sender) =>
        (await sender.Send(new ChangeAuctionStatusCommand(id, request.StatusId))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("ChangeAuctionStatus").Produces<AuctionActionResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Change Auction Status").WithDescription("Announced, Conducted, Successful, Unsuccessful ...");

    auctions.MapPost("/{id:guid}/bids", async (Guid id, RecordBidRequest request, ISender sender) =>
        (await sender.Send(new RecordBidCommand(id, request.BidderOwnerId, request.BidAmount, request.EarnestMoney, request.Remarks))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("RecordBid").Produces<RecordBidCommandResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Record Bid").WithDescription("Bid ranks are recalculated on every bid (highest = 1).");

    auctions.MapPost("/{id:guid}/award", async (Guid id, AwardAuctionRequest request, ISender sender) =>
        (await sender.Send(new AwardAuctionCommand(id, request.AwardDate, request.WinningBidId, request.SuccessfulBidderOwnerId, request.WinningBidAmount, request.AwardReferenceNo))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Approve)
      .WithName("AwardAuction").Produces<AuctionActionResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Award Auction")
      .WithDescription("With recorded bids: winningBidId (default the highest). Without: successfulBidderOwnerId + winningBidAmount. The winning bid must reach the base reserve price.");

    auctions.MapPost("/{id:guid}/cancel", async (Guid id, RemarksRequest request, ISender sender) =>
        (await sender.Send(new CancelAuctionCommand(id, request.Remarks))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("CancelAuction").Produces<AuctionActionResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Cancel Auction");
  }

  // =====================================================
  // OUTSOURCING
  // =====================================================

  private static void MapOutsourcing(IEndpointRouteBuilder app)
  {
    app.MapPost("/properties/{id:guid}/outsourcing-contracts", async (Guid id, CreateOutsourcingRequest request, ISender sender) =>
        (await sender.Send(new CreateOutsourcingCommand(id, request.OutsourcedPartyOwnerId, request.Terms))).ToCreated(r => $"/outsourcing-contracts/{r.Id}"))
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithTags("Outsourcing").WithName("CreateOutsourcing")
      .Produces<CreateOutsourcingCommandResult>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Create Outsourcing Contract").WithDescription("CON-00001 is generated; the contract starts ACTIVE.");

    app.MapGet("/properties/{id:guid}/outsourcing-contracts", async (Guid id, ISender sender) =>
        (await sender.Send(new GetPropertyOutsourcingsQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithTags("Outsourcing").WithName("GetPropertyOutsourcings").Produces<GetOutsourcingsQueryResult>()
      .WithSummary("Get Outsourcing Contracts");

    var contracts = app.MapGroup("/outsourcing-contracts").WithTags("Outsourcing");

    contracts.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetOutsourcingQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetOutsourcing").Produces<GetOutsourcingQueryResult>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Outsourcing Contract");

    contracts.MapPut("/{id:guid}", async (Guid id, OutsourcingTermsInput terms, ISender sender) =>
        (await sender.Send(new UpdateOutsourcingCommand(id, terms))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("UpdateOutsourcing").Produces<OutsourcingActionResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Update Outsourcing Contract");

    contracts.MapPost("/{id:guid}/status", async (Guid id, ChangeMasterStatusRequest request, ISender sender) =>
        (await sender.Send(new ChangeContractStatusCommand(id, request.StatusId))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("ChangeContractStatus").Produces<OutsourcingActionResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Change Contract Status");

    contracts.MapPost("/{id:guid}/terminate", async (Guid id, EndAgreementRequest request, ISender sender) =>
        (await sender.Send(new TerminateOutsourcingCommand(id, request.TerminationDate, request.Reason))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Approve)
      .WithName("TerminateOutsourcing").Produces<OutsourcingActionResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Terminate Outsourcing Contract");
  }
}
