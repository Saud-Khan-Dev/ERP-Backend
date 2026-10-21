public sealed record OpenTaxFileRequest(Guid PropertyId, TaxCategory? TaxCategory, int? Rooms, DateOnly? RegisteredOn, UpliftRateBasis? UpliftRateBasis, decimal? UpliftPct,
  string? OldReferenceNo, string? PlotNo, string? Remarks, string? ExemptionReason);
public sealed record CategoryRequest(TaxCategory TaxCategory, string? Reason);
public sealed record StatusRequest(TaxStatus Status, string? Reason);
public sealed record AddresseeRequest(Guid PayerId);
public sealed record HeadValueRequest(DateOnly? FacilityFrom, decimal? Quantity);
public sealed record OverrideRequest(Guid HeadId, OverrideMode Mode, Guid? PolicyId, decimal? Amount, bool? FollowsIncrease, string Reason, string? Reference, DateOnly EffectiveFrom);
public sealed record EndOverrideRequest(DateOnly? LastDay);
public sealed record HoldRequest(string Reason, string? Reference, Guid? SourceLitigationId, DateOnly StartedOn, bool? SuspendsDemand, bool? ExcludesCompletionClock);
public sealed record EndHoldRequest(DateOnly EndedOn);
public sealed record AgreementTermsRequest(decimal StepPct, int StepIntervalYears, bool IsDirect99Year, DateOnly? NextRenewalDate);
public sealed record StepRevisionRequest(DateOnly EffectiveFrom, decimal StepPct, int StepIntervalYears, string? BoardReference);
public sealed record OpeningArrearsRequest(IReadOnlyList<OpeningArrearsRow> Rows);
public sealed record CorrectOpeningArrearsRequest(OpeningArrearsRow Row, string Reason);
public sealed record RemoveReasonRequest(string Reason);

/// Tax files: one per taxable property, opened and refreshed from the Property service, with what Taxation keeps
/// itself (rooms, registration date, facility dates and quantities, overrides, holds, rent schedules).
public class TaxFileEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    var files = app.MapGroup("/tax-files").WithTags("Tax Files");

    files.MapGet("/", async (int? pageIndex, int? pageSize, string? search, Guid? circleId, Guid? townId, string? category, string? status, bool? withDues, Guid? propertyId,
        ISender sender) =>
        (await sender.Send(new GetTaxFilesQuery(QueryParsing.Page(pageIndex, pageSize), search, circleId, townId, QueryParsing.ParseEnum<TaxCategory>(category, "category"),
          QueryParsing.ParseEnum<TaxStatus>(status, "status"), withDues, propertyId))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("GetTaxFiles").Produces<GetTaxFilesQueryResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Tax Files")
      .WithDescription("search: file no., property code, old reference, payer name or 4+ digits of the 1Link reference. withDues: only files that owe. propertyId: the file of a "
        + "Property property. Dues = what is behind the reference.");

    files.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetTaxFileQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("GetTaxFile").Produces<GetTaxFileQueryResult>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Tax File").WithDescription("With the current Property facts, payers, head values, overrides, holds, agreements, dues and the last reference sync.");

    files.MapPost("/", async (OpenTaxFileRequest r, ISender sender) =>
        (await sender.Send(new OpenTaxFileCommand(r.PropertyId, r.TaxCategory,
          new TaxFileDetails(r.Rooms, r.RegisteredOn, r.UpliftRateBasis, r.UpliftPct, r.OldReferenceNo, r.PlotNo, r.Remarks, r.ExemptionReason)))).ToCreated(c => $"/tax-files/{c.Id}"))
      .RequirePermission(PermissionCatalog.Tax.Create)
      .WithName("OpenTaxFile").Produces<CreatedResult>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status503ServiceUnavailable)
      .WithSummary("Open Tax File")
      .WithDescription("Reads the property from Property (town, type, area, building plans, owners, leases, rentals); its town must be in a circle. taxCategory: from the "
        + "property type unless given. Issues the file no. (GDA-<TOWN>-COM-001) and the constant 1Link reference. The caller needs PROPERTY.VIEW too.");

    files.MapPost("/{id:guid}/sync", async (Guid id, ISender sender) => (await sender.Send(new SyncTaxFileCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Edit)
      .WithName("SyncTaxFile").Produces<SyncTaxFileResult>().ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status503ServiceUnavailable)
      .WithSummary("Sync from Property")
      .WithDescription("A new basis when the facts changed (demands keep the basis they used), payers closed and opened after a transfer, new agreements added.");

    files.MapPut("/{id:guid}", async (Guid id, TaxFileDetails details, ISender sender) => (await sender.Send(new UpdateTaxFileCommand(id, details))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Edit)
      .WithName("UpdateTaxFile").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Update Tax File").WithDescription("What Taxation keeps itself: rooms, registration date, uplift settings, old reference, plot no., remarks.");

    files.MapPost("/{id:guid}/category", async (Guid id, CategoryRequest r, ISender sender) => (await sender.Send(new ChangeTaxCategoryCommand(id, r.TaxCategory, r.Reason))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Approve)
      .WithName("ChangeTaxCategory").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Change Category").WithDescription("res | com | exempt (exempt needs the reason).");

    files.MapPost("/{id:guid}/status", async (Guid id, StatusRequest r, ISender sender) => (await sender.Send(new ChangeTaxStatusCommand(id, r.Status, r.Reason))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Approve)
      .WithName("ChangeTaxStatus").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Change Status").WithDescription("active | exempt | closed. A file is closed only when nothing is due.");

    files.MapPost("/{id:guid}/addressee", async (Guid id, AddresseeRequest r, ISender sender) => (await sender.Send(new ChooseAddresseeCommand(id, r.PayerId))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Edit)
      .WithName("ChooseAddressee").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Choose Addressee").WithDescription("The current payer demands and notices are addressed to.");

    files.MapPut("/{id:guid}/head-values/{headId:guid}", async (Guid id, Guid headId, HeadValueRequest r, ISender sender) =>
        (await sender.Send(new SetHeadValueCommand(id, headId, r.FacilityFrom, r.Quantity))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Edit)
      .WithName("SetHeadValue").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Set Head Value")
      .WithDescription("facilityFrom: the date a FACILITY head's facility was taken (water connection); quantity: a CUSTOM head's quantity. Both null removes the value.");

    files.MapPost("/{id:guid}/overrides", async (Guid id, OverrideRequest r, ISender sender) =>
        (await sender.Send(new AddOverrideCommand(id, r.HeadId, r.Mode, r.PolicyId, r.Amount, r.FollowsIncrease ?? true, r.Reason, r.Reference, r.EffectiveFrom))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Approve)
      .WithName("AddOverride").Produces<CreatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Set Override")
      .WithDescription("special: a fixed yearly amount (amount, followsIncrease) or a special policy (policyId); minimum: the computed amount may not fall below amount. "
        + "An open override of the head ends the day before. Applies from the next demand run.");

    files.MapPost("/{id:guid}/overrides/{overrideId:guid}/end", async (Guid id, Guid overrideId, EndOverrideRequest r, ISender sender) =>
        (await sender.Send(new EndOverrideCommand(id, overrideId, r.LastDay))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Approve)
      .WithName("EndOverride").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("End Override");

    files.MapDelete("/{id:guid}/overrides/{overrideId:guid}", async (Guid id, Guid overrideId, ISender sender) =>
        (await sender.Send(new VoidOverrideCommand(id, overrideId))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Approve)
      .WithName("VoidOverride").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Take Back Override").WithDescription("An override from a later date set by mistake, before it is in force; the override it ended runs on.");

    files.MapPost("/{id:guid}/holds", async (Guid id, HoldRequest r, ISender sender) =>
        (await sender.Send(new AddHoldCommand(id, r.Reason, r.Reference, r.SourceLitigationId, r.StartedOn, r.SuspendsDemand ?? true, r.ExcludesCompletionClock ?? true))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Approve)
      .WithName("AddHold").Produces<CreatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Put on Litigation Hold")
      .WithDescription("While open no tax or fee is levied and no notice issued; the held days do not count toward the completion-certificate grace period.");

    files.MapPost("/{id:guid}/holds/{holdId:guid}/end", async (Guid id, Guid holdId, EndHoldRequest r, ISender sender) =>
        (await sender.Send(new EndHoldCommand(id, holdId, r.EndedOn))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Approve)
      .WithName("EndHold").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("End Hold");

    files.MapDelete("/{id:guid}/holds/{holdId:guid}", async (Guid id, Guid holdId, ISender sender) => (await sender.Send(new VoidHoldCommand(id, holdId))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Approve)
      .WithName("VoidHold").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Take Back Hold").WithDescription("A hold from a later date put by mistake, before it begins.");

    files.MapPut("/{id:guid}/agreements/{agreementId:guid}", async (Guid id, Guid agreementId, AgreementTermsRequest r, ISender sender) =>
        (await sender.Send(new SetAgreementTermsCommand(id, agreementId, r.StepPct, r.StepIntervalYears, r.IsDirect99Year, r.NextRenewalDate))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Edit)
      .WithName("SetAgreementTerms").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Set Agreement Terms").WithDescription("The step-up of the rent or lease amount and how often it applies (Property has no interval).");

    files.MapPost("/{id:guid}/agreements/{agreementId:guid}/revisions", async (Guid id, Guid agreementId, StepRevisionRequest r, ISender sender) =>
        (await sender.Send(new ReviseAgreementStepCommand(id, agreementId, r.EffectiveFrom, r.StepPct, r.StepIntervalYears, r.BoardReference))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Approve)
      .WithName("ReviseAgreementStep").Produces<CreatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Revise Step Mid-term").WithDescription("15 % revised to 7 % after two years: past years keep the old step.");

    files.MapPost("/{id:guid}/opening-arrears", async (Guid id, OpeningArrearsRequest r, ISender sender) => (await sender.Send(new AddOpeningArrearsCommand(id, r.Rows))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Create)
      .WithName("AddOpeningArrears").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Add Opening Arrears")
      .WithDescription("The old register's arrears by head code (and year when known), before the file's first demand: paid against the reference until "
        + "then, and billed on it. The reference is re-synced.");

    files.MapGet("/{id:guid}/opening-arrears", async (Guid id, ISender sender) => (await sender.Send(new GetOpeningArrearsQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("GetOpeningArrears").Produces<GetOpeningArrearsQueryResult>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Opening Arrears").WithDescription("The old register's dues on the file, billed or not; correctable: not billed yet.");

    files.MapPut("/{id:guid}/opening-arrears/{arrearsId:guid}", async (Guid id, Guid arrearsId, CorrectOpeningArrearsRequest r, ISender sender) =>
        (await sender.Send(new CorrectOpeningArrearsCommand(id, arrearsId, r.Row, r.Reason))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Edit)
      .WithName("CorrectOpeningArrears").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Correct Opening Arrears").WithDescription("Not billed yet; never below what was already paid on it. The reason is kept.");

    files.MapPost("/{id:guid}/opening-arrears/{arrearsId:guid}/remove", async (Guid id, Guid arrearsId, RemoveReasonRequest r, ISender sender) =>
        (await sender.Send(new RemoveOpeningArrearsCommand(id, arrearsId, r.Reason))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Delete)
      .WithName("RemoveOpeningArrears").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Remove Opening Arrears").WithDescription("Entered by mistake: not billed and nothing paid on it.");

    files.MapGet("/{id:guid}/assessment", async (Guid id, int fiscalYear, ISender sender) => (await sender.Send(new PreviewAssessmentQuery(id, fiscalYear))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("PreviewAssessment").Produces<AssessmentDto>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Preview Assessment").WithDescription("The year's computation for the file (fiscalYear 2026 = FY 2026-27), with how each head was worked out. Nothing is saved.");

    files.MapGet("/{id:guid}/demands", async (Guid id, ISender sender) => (await sender.Send(new GetFileDemandsQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("GetFileDemands").Produces<GetFileDemandsQueryResult>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get File Demands").WithDescription("Every year's demand, newest first, and what is behind the reference.");

    app.MapGet("/overrides", async (int? pageIndex, int? pageSize, Guid? headId, Guid? policyId, string? mode, bool? inForce, Guid? circleId, string? search, ISender sender) =>
        (await sender.Send(new GetOverridesQuery(QueryParsing.Page(pageIndex, pageSize), headId, policyId, QueryParsing.ParseEnum<OverrideMode>(mode, "mode"), inForce, circleId,
          search))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithTags("Tax Files").WithName("GetOverrides").Produces<GetOverridesQueryResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Overrides Register")
      .WithDescription("Every override on every file, newest first: mode = special | minimum; inForce: in force today (or not); search: file no., property code, reason or reference.");

    app.MapGet("/reference-syncs", async (int? pageIndex, int? pageSize, Guid? taxFileId, string? status, string? reason, string? search, DateOnly? from, DateOnly? to,
        ISender sender) =>
        (await sender.Send(new GetReferenceSyncsQuery(QueryParsing.Page(pageIndex, pageSize), taxFileId, QueryParsing.ParseEnum<PsidSyncStatus>(status, "status"),
          QueryParsing.ParseEnum<PsidSyncReason>(reason, "reason"), search, from, to))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithTags("Payments").WithName("GetReferenceSyncs").Produces<GetReferenceSyncsQueryResult>()
      .WithSummary("Get Reference Syncs")
      .WithDescription("The amount behind each constant reference after every change. No 1Link connector exists yet, so they stay pending. status = pending | "
        + "acked | failed; reason = annual_demand | penalty_add_up | override | payment | correction | reversal; search: file no., reference or note.");
  }
}
