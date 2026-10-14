public sealed record ActivationRequest(bool IsActive);
public sealed record AssignTownRequest(Guid TownId, string? ShortCode, DateOnly EffectiveFrom);
public sealed record ShortCodeRequest(string? ShortCode);
public sealed record CircleStaffRequest(Guid? CircleId, Guid PostId, TaxRole TaxRole, DateOnly EffectiveFrom);
public sealed record EndRequest(DateOnly LastDay);
public sealed record CreateCircleRequest(string Code, string Name, Guid? OrgUnitId, Guid? OfficerPostId, string? OfficeAddress, string? OfficeFax, string? OfficePhone, int? SortOrder);
public sealed record CreateRegularHeadRequest(string? Code, RegularHeadInput Head);
public sealed record CreateOtherHeadRequest(string Code, OtherHeadInput Head);
public sealed record CategoryMapRequest(Guid PropertyTypeId, TaxCategory TaxCategory);
public sealed record CreateRateSetRequest(RateSetInput RateSet, Guid? CopyFromId);
public sealed record LtfFactorsRequest(Guid TownId, TaxCategory TaxCategory, LtfComponent Component, IReadOnlyList<BandInput> Bands);
public sealed record HeadRatesRequest(Guid HeadId, TaxCategory? TaxCategory, Guid? TownId, HeadRateInput Rates);
public sealed record ChangeSettingsRequest(DateOnly EffectiveFrom, IReadOnlyDictionary<string, string> Values, Guid? RateSetId);
public sealed record MessageTemplateRequest(string Body, bool IsActive);
public sealed record ReviseLetterheadRequest(LetterheadInput Letterhead, string? LogoLeftRef, string? LogoRightRef, DateOnly? EffectiveFrom);
public sealed record ReviseTableLayoutRequest(TableLayoutInput Layout, DateOnly? EffectiveFrom);

/// Taxation setup: circles and their towns and staff, tax heads, category mapping, rate sets (locality factors, head
/// rates, approval), settings, special policies, penalty rules, SMS texts, notice templates, the letterhead and the
/// notice table. Anyone in Taxation may read it; changes need TAX_SETUP.
public class SetupEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    // ---- circles ----

    var circles = app.MapGroup("/tax-circles").WithTags("Circles");

    circles.MapGet("/", async (bool? includeInactive, ISender sender) => (await sender.Send(new GetCirclesQuery(includeInactive ?? false))).ToOk())
      .RequireAnyPermission(TaxAuthorization.AnyTaxReader)
      .WithName("GetCircles").Produces<GetCirclesQueryResult>()
      .WithSummary("Get Circles").WithDescription("With their towns (current and past assignments) and the HRM posts working for them.");

    circles.MapPost("/", async (CreateCircleRequest r, ISender sender) =>
        (await sender.Send(new CreateCircleCommand(r.Code, new CircleInput(r.Name, r.OrgUnitId, r.OfficerPostId, r.OfficeAddress, r.OfficeFax, r.OfficePhone, r.SortOrder ?? 0))))
          .ToCreated(c => $"/tax-circles/{c.Id}"))
      .RequirePermission(PermissionCatalog.TaxSetup.Create)
      .WithName("CreateCircle").Produces<CreatedResult>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Create Circle").WithDescription("officerPostId: the HRM post of the Taxation Officer who signs the circle's notices. The office lines are printed under the letterhead.");

    circles.MapPut("/{id:guid}", async (Guid id, CircleInput circle, ISender sender) => (await sender.Send(new UpdateCircleCommand(id, circle))).ToOk())
      .RequirePermission(PermissionCatalog.TaxSetup.Edit)
      .WithName("UpdateCircle").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update Circle");

    foreach (var (route, active) in new[] { ("activate", true), ("deactivate", false) })
    {
      circles.MapPost($"/{{id:guid}}/{route}", async (Guid id, ISender sender) => (await sender.Send(new SetCircleActivationCommand(id, active))).ToOk())
        .RequirePermission(PermissionCatalog.TaxSetup.Edit)
        .WithName(active ? "ActivateCircle" : "DeactivateCircle").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary(active ? "Activate Circle" : "Deactivate Circle");
    }

    circles.MapPost("/{id:guid}/towns", async (Guid id, AssignTownRequest r, ISender sender) =>
        (await sender.Send(new AssignTownCommand(id, r.TownId, r.ShortCode, r.EffectiveFrom))).ToOk())
      .RequirePermission(PermissionCatalog.TaxSetup.Edit)
      .WithName("AssignTown").Produces<AssignTownResult>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status503ServiceUnavailable)
      .WithSummary("Assign Town to Circle")
      .WithDescription("townId: a town of Property's master (checked there). The town's assignment in force on the date ends the day before; its tax files move "
        + "with it once the date has come. shortCode: printed as \"Town\" on notices (NG).");

    circles.MapPut("/{id:guid}/towns/{assignmentId:guid}", async (Guid id, Guid assignmentId, ShortCodeRequest r, ISender sender) =>
        (await sender.Send(new SetTownShortCodeCommand(id, assignmentId, r.ShortCode))).ToOk())
      .RequirePermission(PermissionCatalog.TaxSetup.Edit)
      .WithName("SetTownShortCode").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Set Town Short Code");

    app.MapPost("/tax-circle-staff", async (CircleStaffRequest r, ISender sender) =>
        (await sender.Send(new AddCircleStaffCommand(r.CircleId, r.PostId, r.TaxRole, r.EffectiveFrom))).ToCreated(c => $"/tax-circle-staff/{c.Id}"))
      .RequirePermission(PermissionCatalog.TaxSetup.Edit)
      .WithTags("Circles").WithName("AddCircleStaff").Produces<CreatedResult>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Add Post to Circle")
      .WithDescription("An HRM post that works for a circle (TO, ATO, TC) or for the whole Section (DD_TAX, no circle). The person is whoever holds the post on the date.");

    app.MapPost("/tax-circle-staff/{id:guid}/end", async (Guid id, EndRequest r, ISender sender) => (await sender.Send(new EndCircleStaffCommand(id, r.LastDay))).ToOk())
      .RequirePermission(PermissionCatalog.TaxSetup.Edit)
      .WithTags("Circles").WithName("EndCircleStaff").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("End Post in Circle");

    // ---- heads ----

    var heads = app.MapGroup("/tax-heads").WithTags("Tax Heads");

    heads.MapGet("/", async (string? kind, bool? includeInactive, ISender sender) =>
        (await sender.Send(new GetHeadsQuery(QueryParsing.ParseEnum<HeadKind>(kind, "kind"), includeInactive ?? false))).ToOk())
      .RequireAnyPermission(TaxAuthorization.AnyTaxReader)
      .WithName("GetHeads").Produces<GetHeadsQueryResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Tax Heads").WithDescription("kind = regular (the annual demand, in computing order) | other (fees and charges added on a notice).");

    heads.MapPost("/regular", async (CreateRegularHeadRequest r, ISender sender) => (await sender.Send(new CreateRegularHeadCommand(r.Code, r.Head))).ToCreated(c => $"/tax-heads/{c.Id}"))
      .RequirePermission(PermissionCatalog.TaxSetup.Create)
      .WithName("CreateRegularHead").Produces<CreatedResult>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Create Regular Head")
      .WithDescription("A head of the annual demand defined by the admin (e.g. signboard fee): calcMethod bands | rate | fixed | formula; chargedFrom always | registration | "
        + "facility (with facilityLabel). Its rates are entered in a draft rate set. The head gets its own row on the notice table.");

    heads.MapPut("/{id:guid}/regular", async (Guid id, RegularHeadInput head, ISender sender) => (await sender.Send(new UpdateRegularHeadCommand(id, head))).ToOk())
      .RequirePermission(PermissionCatalog.TaxSetup.Edit)
      .WithName("UpdateRegularHead").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update Regular Head")
      .WithDescription("Property tax and annual rent are built in: only their name, flags and order change (and the property tax formula, formulaCom / formulaRes).");

    heads.MapPost("/other", async (CreateOtherHeadRequest r, ISender sender) => (await sender.Send(new CreateOtherHeadCommand(r.Code, r.Head))).ToCreated(c => $"/tax-heads/{c.Id}"))
      .RequirePermission(PermissionCatalog.TaxSetup.Create)
      .WithName("CreateOtherHead").Produces<CreatedResult>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Create Fee or Other Head");

    heads.MapPut("/{id:guid}/other", async (Guid id, OtherHeadInput head, ISender sender) => (await sender.Send(new UpdateOtherHeadCommand(id, head))).ToOk())
      .RequirePermission(PermissionCatalog.TaxSetup.Edit)
      .WithName("UpdateOtherHead").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update Fee or Other Head");

    foreach (var (route, active) in new[] { ("activate", true), ("deactivate", false) })
    {
      heads.MapPost($"/{{id:guid}}/{route}", async (Guid id, ISender sender) => (await sender.Send(new SetHeadActivationCommand(id, active))).ToOk())
        .RequirePermission(PermissionCatalog.TaxSetup.Edit)
        .WithName(active ? "ActivateHead" : "DeactivateHead").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary(active ? "Activate Head" : "Deactivate Head");
    }

    // ---- category mapping ----

    var maps = app.MapGroup("/category-maps").WithTags("Tax Heads");

    maps.MapGet("/", async (ISender sender) => (await sender.Send(new GetCategoryMapsQuery())).ToOk())
      .RequireAnyPermission(TaxAuthorization.AnyTaxReader)
      .WithName("GetCategoryMaps").Produces<GetCategoryMapsQueryResult>()
      .WithSummary("Get Category Mapping")
      .WithDescription("Which tax category each Property property type is assessed in. Unmapped RESIDENTIAL and COMMERCIAL types map by their code.");

    maps.MapPut("/", async (CategoryMapRequest r, ISender sender) => (await sender.Send(new SetCategoryMapCommand(r.PropertyTypeId, r.TaxCategory))).ToOk())
      .RequirePermission(PermissionCatalog.TaxSetup.Edit)
      .WithName("SetCategoryMap").Produces<CreatedResult>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status503ServiceUnavailable)
      .WithSummary("Map Property Type").WithDescription("taxCategory = res | com | exempt.");

    maps.MapDelete("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new DeleteCategoryMapCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.TaxSetup.Delete)
      .WithName("DeleteCategoryMap").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Remove Mapping");

    // ---- rate sets ----

    var sets = app.MapGroup("/rate-sets").WithTags("Rate Sets");

    sets.MapGet("/", async (ISender sender) => (await sender.Send(new GetRateSetsQuery())).ToOk())
      .RequireAnyPermission(TaxAuthorization.AnyTaxReader)
      .WithName("GetRateSets").Produces<GetRateSetsQueryResult>()
      .WithSummary("Get Rate Sets").WithDescription("Newest first. A demand run uses the approved set in force on 1 July of its year.");

    sets.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetRateSetQuery(id))).ToOk())
      .RequireAnyPermission(TaxAuthorization.AnyTaxReader)
      .WithName("GetRateSet").Produces<GetRateSetQueryResult>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Rate Set").WithDescription("Its locality factors by town, category and component, and the rates of every head.");

    sets.MapPost("/", async (CreateRateSetRequest r, ISender sender) => (await sender.Send(new CreateRateSetCommand(r.RateSet, r.CopyFromId))).ToCreated(c => $"/rate-sets/{c.Id}"))
      .RequirePermission(PermissionCatalog.TaxSetup.Create)
      .WithName("CreateRateSet").Produces<CreatedResult>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Create Rate Set").WithDescription("A draft, empty or with the factors and rates of copyFromId.");

    sets.MapPut("/{id:guid}", async (Guid id, RateSetInput r, ISender sender) => (await sender.Send(new UpdateRateSetCommand(id, r))).ToOk())
      .RequirePermission(PermissionCatalog.TaxSetup.Edit)
      .WithName("UpdateRateSet").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Update Rate Set").WithDescription("Drafts only.");

    sets.MapDelete("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new DeleteRateSetCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.TaxSetup.Delete)
      .WithName("DeleteRateSet").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Delete Draft Rate Set");

    sets.MapPut("/{id:guid}/ltf-factors", async (Guid id, LtfFactorsRequest r, ISender sender) =>
        (await sender.Send(new SetLtfFactorsCommand(id, r.TownId, r.TaxCategory, r.Component, r.Bands))).ToOk())
      .RequirePermission(PermissionCatalog.TaxSetup.Edit)
      .WithName("SetLtfFactors").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Set Locality Factors")
      .WithDescription("A town's factors for one category (res | com) and component (land per sq yd | covered per sq ft): bands [{upTo, value}] with rising limits, "
        + "only the last open (upTo null).");

    sets.MapDelete("/{id:guid}/ltf-factors/{townId:guid}", async (Guid id, Guid townId, ISender sender) => (await sender.Send(new RemoveTownFactorsCommand(id, townId))).ToOk())
      .RequirePermission(PermissionCatalog.TaxSetup.Edit)
      .WithName("RemoveTownFactors").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Remove Town Factors");

    sets.MapPut("/{id:guid}/head-rates", async (Guid id, HeadRatesRequest r, ISender sender) =>
        (await sender.Send(new SetHeadRatesCommand(id, r.HeadId, r.TaxCategory, r.TownId, r.Rates))).ToOk())
      .RequirePermission(PermissionCatalog.TaxSetup.Edit)
      .WithName("SetHeadRates").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Set Head Rates")
      .WithDescription("By the head's calculation: bands (basis, bands, blockSize, roundUp, aboveTopRule), rate (basis, rate), fixed (amount), formula (basis of QTY). "
        + "A fee gives basis, rateType (flat | per_unit | percent) and rate, or bands; townId for a town's own rate (transfer fee).");

    sets.MapDelete("/{id:guid}/head-rates", async (Guid id, Guid headId, string? taxCategory, Guid? townId, ISender sender) =>
        (await sender.Send(new RemoveHeadRatesCommand(id, headId, QueryParsing.ParseEnum<TaxCategory>(taxCategory, "taxCategory"), townId))).ToOk())
      .RequirePermission(PermissionCatalog.TaxSetup.Edit)
      .WithName("RemoveHeadRates").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Remove Head Rates");

    sets.MapPost("/{id:guid}/approve", async (Guid id, ISender sender) => (await sender.Send(new ApproveRateSetCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.TaxSetup.Approve)
      .WithName("ApproveRateSet").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Approve Rate Set")
      .WithDescription("Needs rates for every active configured regular head and locality factors for every town in a circle. The set it replaces ends the day before.");

    // ---- settings ----

    app.MapGet("/tax-settings", async (DateOnly? asOf, ISender sender) => (await sender.Send(new GetSettingsQuery(asOf))).ToOk())
      .RequireAnyPermission(TaxAuthorization.AnyTaxReader)
      .WithTags("Settings").WithName("GetSettings").Produces<GetSettingsQueryResult>()
      .WithSummary("Get Settings").WithDescription("Every setting in force on the date (today by default), with its default and what it does.");

    app.MapPut("/tax-settings", async (ChangeSettingsRequest r, ISender sender) => (await sender.Send(new ChangeSettingsCommand(r.EffectiveFrom, r.Values, r.RateSetId))).ToOk())
      .RequirePermission(PermissionCatalog.TaxSetup.Edit)
      .WithTags("Settings").WithName("ChangeSettings").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Change Settings")
      .WithDescription("values: {KEY: value}. Each key's value in force ends the day before effectiveFrom; on the same date it is corrected. rateSetId: the Board decision.");

    // ---- special policies ----

    var policies = app.MapGroup("/special-policies").WithTags("Special Policies");

    policies.MapGet("/", async (Guid? headId, bool? includeInactive, ISender sender) => (await sender.Send(new GetSpecialPoliciesQuery(headId, includeInactive ?? true))).ToOk())
      .RequireAnyPermission(TaxAuthorization.AnyTaxReader)
      .WithName("GetSpecialPolicies").Produces<GetSpecialPoliciesQueryResult>()
      .WithSummary("Get Special Policies");

    policies.MapPost("/", async (SpecialPolicyRequest r, ISender sender) => (await sender.Send(new CreateSpecialPolicyCommand(r.HeadId, r.Policy))).ToCreated(c => $"/special-policies/{c.Id}"))
      .RequirePermission(PermissionCatalog.TaxSetup.Create)
      .WithName("CreateSpecialPolicy").Produces<CreatedResult>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Create Special Policy")
      .WithDescription("method = fixed | per_month | percent_of_normal | per_room | per_sqft | per_kanal. The head must allow overrides.");

    policies.MapPut("/{id:guid}", async (Guid id, SpecialPolicyRequest r, ISender sender) => (await sender.Send(new UpdateSpecialPolicyCommand(id, r.HeadId, r.Policy))).ToOk())
      .RequirePermission(PermissionCatalog.TaxSetup.Edit)
      .WithName("UpdateSpecialPolicy").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update Special Policy");

    foreach (var (route, active) in new[] { ("activate", true), ("deactivate", false) })
    {
      policies.MapPost($"/{{id:guid}}/{route}", async (Guid id, ISender sender) => (await sender.Send(new SetSpecialPolicyActivationCommand(id, active))).ToOk())
        .RequirePermission(PermissionCatalog.TaxSetup.Edit)
        .WithName(active ? "ActivateSpecialPolicy" : "DeactivateSpecialPolicy").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary(active ? "Activate Special Policy" : "Deactivate Special Policy");
    }

    // ---- penalty rules, SMS texts, notice templates ----

    app.MapGet("/penalty-rules", async (ISender sender) => (await sender.Send(new GetPenaltyRulesQuery())).ToOk())
      .RequireAnyPermission(TaxAuthorization.AnyTaxReader)
      .WithTags("Penalties").WithName("GetPenaltyRules").Produces<GetPenaltyRulesQueryResult>()
      .WithSummary("Get Penalty Rules");

    app.MapPut("/penalty-rules/{id:guid}", async (Guid id, PenaltyRuleInput rule, ISender sender) => (await sender.Send(new UpdatePenaltyRuleCommand(id, rule))).ToOk())
      .RequirePermission(PermissionCatalog.TaxSetup.Edit)
      .WithTags("Penalties").WithName("UpdatePenaltyRule").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Update Penalty Rule")
      .WithDescription("rateType percent | fixed; the in-year rule also has the period (periodValue, periodUnit months | days, periodAnchor fy_start | demand_issue | due_date).");

    app.MapGet("/message-templates", async (ISender sender) => (await sender.Send(new GetMessageTemplatesQuery())).ToOk())
      .RequireAnyPermission(TaxAuthorization.AnyTaxReader)
      .WithTags("Notices Setup").WithName("GetMessageTemplates").Produces<GetMessageTemplatesQueryResult>()
      .WithSummary("Get SMS Texts");

    app.MapPut("/message-templates/{id:guid}", async (Guid id, MessageTemplateRequest r, ISender sender) => (await sender.Send(new UpdateMessageTemplateCommand(id, r.Body, r.IsActive))).ToOk())
      .RequirePermission(PermissionCatalog.TaxSetup.Edit)
      .WithTags("Notices Setup").WithName("UpdateMessageTemplate").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Update SMS Text").WithDescription("Placeholders: {name} {code} {fy} {total} {arrears} {balance} {psid} {due} {replyBy} {surcharge} {pct} {amount}.");

    app.MapGet("/notice-templates", async (ISender sender) => (await sender.Send(new GetNoticeTemplatesQuery())).ToOk())
      .RequireAnyPermission(TaxAuthorization.AnyTaxReader)
      .WithTags("Notices Setup").WithName("GetNoticeTemplates").Produces<GetNoticeTemplatesQueryResult>()
      .WithSummary("Get Notice Templates");

    app.MapPut("/notice-templates/{stage}", async (string stage, NoticeTemplateInput template, ISender sender) =>
        (await sender.Send(new UpdateNoticeTemplateCommand(QueryParsing.ParseEnum<NoticeStage>(stage, "stage")!.Value, template))).ToOk())
      .RequirePermission(PermissionCatalog.TaxSetup.Edit)
      .WithTags("Notices Setup").WithName("UpdateNoticeTemplate").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Update Notice Template")
      .WithDescription("stage = first | second | final | show_cause | surcharge. A changed title or paragraph raises the version; issued notices keep theirs.");

    // ---- letterhead and notice table ----

    app.MapGet("/letterhead", async (ISender sender) => (await sender.Send(new GetLetterheadQuery())).ToOk())
      .RequireAnyPermission(TaxAuthorization.AnyTaxReader)
      .WithTags("Notices Setup").WithName("GetLetterhead").Produces<GetLetterheadQueryResult>()
      .WithSummary("Get Letterhead");

    app.MapPut("/letterhead", async (ReviseLetterheadRequest r, ISender sender) =>
        (await sender.Send(new ReviseLetterheadCommand(r.Letterhead, r.LogoLeftRef, r.LogoRightRef, r.EffectiveFrom))).ToOk())
      .RequirePermission(PermissionCatalog.TaxSetup.Edit)
      .WithTags("Notices Setup").WithName("ReviseLetterhead").Produces<CreatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Revise Letterhead")
      .WithDescription("A new letterhead from effectiveFrom (today by default). logoLeftRef / logoRightRef: the fileRef of an uploaded logo, \"\" to remove, null to keep.");

    app.MapPost("/letterhead/logos", async (IFormFile? file, ISender sender) =>
      {
        if (file is null)
          return Results.BadRequest(new { Message = "Attach the logo as 'file'." });
        await using var stream = file.OpenReadStream();
        return (await sender.Send(new UploadLogoCommand(stream, file.FileName, file.Length))).ToOk();
      })
      .RequirePermission(PermissionCatalog.TaxSetup.Edit)
      .DisableAntiforgery()
      .Accepts<IFormFile>("multipart/form-data")
      .WithTags("Notices Setup").WithName("UploadLogo").Produces<UploadLogoResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Upload Logo").WithDescription("PNG, JPG, SVG or WEBP up to 1 MB. Returns the fileRef to pass to Revise Letterhead.");

    app.MapGet("/letterheads/{id:guid}/logo/{side}", async (Guid id, string side, ISender sender) =>
      {
        var result = await sender.Send(new GetLogoQuery(id, side));
        return result.IsSuccess ? Results.File(result.Value!.Content, result.Value.ContentType, result.Value.FileName) : Results.BadRequest(new { result.Message });
      })
      .RequireAnyPermission(TaxAuthorization.AnyTaxReader)
      .WithTags("Notices Setup").WithName("GetLogo").ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Logo").WithDescription("side = left (GDA logo, also the watermark) | right (KP emblem).");

    app.MapGet("/notice-table-layout", async (ISender sender) => (await sender.Send(new GetTableLayoutQuery())).ToOk())
      .RequireAnyPermission(TaxAuthorization.AnyTaxReader)
      .WithTags("Notices Setup").WithName("GetTableLayout").Produces<GetTableLayoutQueryResult>()
      .WithSummary("Get Notice Table").WithDescription("The rows (label and the heads each adds up), the columns, and the heads to choose from.");

    app.MapPut("/notice-table-layout", async (ReviseTableLayoutRequest r, ISender sender) => (await sender.Send(new ReviseTableLayoutCommand(r.Layout, r.EffectiveFrom))).ToOk())
      .RequirePermission(PermissionCatalog.TaxSetup.Edit)
      .WithTags("Notices Setup").WithName("ReviseTableLayout").Produces<CreatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Revise Notice Table")
      .WithDescription("rows [{label, headCodes}]: a head is in at most one row; a head in no row prints on the otherLabel row, so the grand total always equals "
        + "the amount due. columns [{key, heading, printedByDefault}] ({fy} = the year, | = a new line); at least one amount column.");
  }
}
