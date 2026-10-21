using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// Where the other services are. Locally the ports of launchSettings; in compose the container names.
public sealed class ServiceEndpointsOptions
{
  public const string SectionName = "Services";

  public string? PropertyBaseUrl { get; set; }
  public string? HrmBaseUrl { get; set; }
  public int TimeoutSeconds { get; set; } = 20;
}

/// Passes the signed-in user's token on, so Property and HRM apply that user's own permissions.
public sealed class ForwardAuthorizationHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
  {
    var header = accessor.HttpContext?.Request.Headers.Authorization.ToString();
    if (!string.IsNullOrEmpty(header))
      request.Headers.TryAddWithoutValidation("Authorization", header);
    return base.SendAsync(request, cancellationToken);
  }
}

/// JSON reads shared by the clients: camelCase, enums as names.
internal static class ServiceJson
{
  public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

  /// GET a JSON body; null on 404. Anything else that is not a success becomes an ExternalServiceException with a
  /// sentence the user can act on.
  public static async Task<T?> GetAsync<T>(HttpClient http, string service, string path, ILogger logger, CancellationToken cancellationToken) where T : class
  {
    if (http.BaseAddress is null)
      throw new ExternalServiceException($"The {service} service address is not configured (Services:{service}BaseUrl).");

    HttpResponseMessage response;
    try
    {
      response = await http.GetAsync(path, cancellationToken);
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
    {
      logger.LogWarning(ex, "{Service} did not answer {Path}", service, path);
      throw new ExternalServiceException($"The {service} service is not reachable. Try again, or check that it is running.", ex);
    }

    using (response)
    {
      if (response.StatusCode == HttpStatusCode.NotFound)
        return null;
      if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        throw new ExternalServiceException($"The {service} service refused the request: the signed-in user needs read access there ({(service == "Property" ? "PROPERTY.VIEW" : "an HRM permission")}).");
      if (!response.IsSuccessStatusCode)
      {
        logger.LogWarning("{Service} answered {Status} to {Path}", service, (int)response.StatusCode, path);
        throw new ExternalServiceException($"The {service} service answered {(int)response.StatusCode} to {path}.");
      }
      return await response.Content.ReadFromJsonAsync<T>(Options, cancellationToken);
    }
  }
}

/// Reads properties, their areas, plans, owners and agreements from the Property service.
public sealed class PropertyServiceClient(HttpClient http, ILogger<PropertyServiceClient> logger) : IPropertyDirectory
{
  private const string Service = "Property";

  public async Task<PropertySnapshot> SnapshotAsync(Guid propertyId, CancellationToken cancellationToken)
  {
    var p = (await Get<PropertyEnvelope>($"properties/{propertyId}", cancellationToken))?.Property
      ?? throw new PropertyNotFoundException($"Property {propertyId} was not found in the Property service.");

    var measurements = (await Get<MeasurementsEnvelope>($"properties/{propertyId}/measurements", cancellationToken))?.Measurements ?? [];
    var plans = (await Get<PlansEnvelope>($"properties/{propertyId}/building-plans", cancellationToken))?.Plans ?? [];
    var leases = (await Get<LeasesEnvelope>($"properties/{propertyId}/leases", cancellationToken))?.Leases ?? [];
    var rentals = (await Get<RentalsEnvelope>($"properties/{propertyId}/rentals", cancellationToken))?.Rentals ?? [];
    var allotments = (await Get<AllotmentsEnvelope>($"properties/{propertyId}/allotments", cancellationToken))?.Allotments ?? [];
    var units = (await Get<MastersEnvelope>("masters/measurement-units?includeInactive=true", cancellationToken))?.Items ?? [];

    // the measured land; the covered area of the latest approved building plan, else the measured built-up area
    var current = measurements.FirstOrDefault(m => m.IsCurrent);
    var approved = plans.Where(pl => Code(pl.Status) == "APPROVED" && Code(pl.PlanType) != "COMPLETION_PLAN" && pl.ApprovalDate is not null)
      .OrderByDescending(pl => pl.ApprovalDate).ThenByDescending(pl => pl.RevisionNo).FirstOrDefault();
    var completion = plans.Where(pl => Code(pl.Status) == "APPROVED" && Code(pl.PlanType) == "COMPLETION_PLAN" && pl.ApprovalDate is not null)
      .OrderByDescending(pl => pl.ApprovalDate).FirstOrDefault();

    decimal? planSqft = null;
    if (approved?.CoveredArea is > 0 && units.FirstOrDefault(u => u.Code == Code(approved.Unit))?.FactorToBase is { } factor)
      planSqft = approved.CoveredArea * factor;
    var (covered, source) = planSqft is > 0 ? (planSqft, CoveredAreaSource.ApprovedPlan)
      : p.Area?.BuiltUpAreaSqFt is > 0 ? (p.Area.BuiltUpAreaSqFt, CoveredAreaSource.Measurement)
      : ((decimal?)null, CoveredAreaSource.None);

    // who pays, and the agreements the rent is billed from
    var activeRental = rentals.FirstOrDefault(r => Code(r.Status) == "ACTIVE");
    var activeLease = leases.FirstOrDefault(l => Code(l.Status) == "ACTIVE");
    var classification = Code(p.Classification);
    var holding = activeRental is not null ? HoldingKind.Rental
      : classification == "GDA_PROPERTY" ? (activeLease is not null ? HoldingKind.GdaLease : HoldingKind.GdaOwnership)
      : HoldingKind.PrivateRegistered;

    var parties = new List<(OwnerRefDto Owner, PayerRole Role, PayerSource Source, Guid SourceId, decimal? Share, DateOnly? From)>();
    foreach (var o in p.CurrentOwners ?? [])
      if (o.Owner is not null)
        parties.Add((o.Owner, PayerRole.Owner, PayerSource.Ownership, o.Id, o.OwnershipSharePct, o.EffectiveFrom));
    if (activeLease?.Lessee is not null)
      parties.Add((activeLease.Lessee, PayerRole.Lessee, PayerSource.Lease, activeLease.Id, null, activeLease.LeaseStartDate));
    if (activeRental?.Tenant is not null)
      parties.Add((activeRental.Tenant, PayerRole.Tenant, PayerSource.Rental, activeRental.Id, null, activeRental.RentalStartDate));
    if (parties.Count == 0 && allotments.FirstOrDefault(a => a.IsInForce) is { Allottee: not null } allotment)
      parties.Add((allotment.Allottee, PayerRole.Allottee, PayerSource.Allotment, allotment.Id, null, allotment.AllotmentDate));

    var owners = new Dictionary<Guid, OwnerDetail?>();
    foreach (var id in parties.Select(x => x.Owner.Id).Distinct())
      owners[id] = (await Get<OwnerEnvelope>($"owners/{id}", cancellationToken))?.Owner;

    var payers = parties.Select(x =>
    {
      var detail = owners.GetValueOrDefault(x.Owner.Id);
      var mobile = detail?.Contacts?.Where(c => c.IsActive).OrderByDescending(c => c.IsPrimary).Select(c => c.ContactNumber).FirstOrDefault();
      var address = detail?.Addresses?.Where(a => a.IsActive)
        .OrderByDescending(a => a.IsPrimary).ThenBy(a => a.AddressType switch { "Mailing" => 0, "Permanent" => 1, "Present" => 2, _ => 3 })
        .Select(a => string.Join(", ", new[] { a.FullAddress, a.CityTown, a.District }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct())).FirstOrDefault();
      return new PayerSnapshot(x.Owner.Id, x.Owner.OwnerCode, x.Role, x.Source, x.SourceId, x.Share, detail?.OwnerName ?? x.Owner.OwnerName,
        detail?.FatherHusbandName, detail?.Cnic ?? detail?.Ntn, mobile, address ?? p.AddressLine, x.From);
    }).ToList();

    var agreements = new List<AgreementSnapshot>();
    foreach (var r in rentals.Where(r => Code(r.Status) is not ("DRAFT" or "CANCELLED")))
      agreements.Add(new AgreementSnapshot(AgreementKind.Rental, r.Id, r.RentalNo, r.RentAmount, r.RentFrequency switch
      {
        "Monthly" => AmountFrequency.Monthly,
        "Quarterly" => AmountFrequency.Quarterly,
        _ => AmountFrequency.Annual
      }, r.RentalStartDate, r.TerminationDate ?? r.RentalEndDate, null, r.AnnualIncreasePct, Code(r.Status) == "ACTIVE"));
    foreach (var l in leases.Where(l => Code(l.Status) is not ("DRAFT" or "CANCELLED") && l.LeaseAmount is > 0))
      agreements.Add(new AgreementSnapshot(AgreementKind.Lease, l.Id, l.LeaseNo, l.LeaseAmount!.Value, l.AmountFrequency switch
      {
        "Monthly" => AmountFrequency.Monthly,
        "OneTime" => AmountFrequency.OneTime,
        _ => AmountFrequency.Annual
      }, l.LeaseStartDate, l.TerminationDate ?? l.LeaseEndDate, l.LeaseTermYears, null, Code(l.Status) == "ACTIVE"));

    return new PropertySnapshot(
      p.Id, p.PropertyCode, p.PropertyName, p.IsActive, p.Town?.Id, p.Town?.Code, p.Town?.Name, p.PropertyType?.Id, Code(p.PropertyType), Code(p.Status), classification,
      p.AddressLine, p.Area?.TotalAreaSqFt, current?.TotalArea, Code(current?.Unit), p.Area?.RegularizedAreaSqFt, covered, source, p.Area?.CurrentMeasurementId,
      approved?.Id, approved?.ApprovalDate, completion?.Id, completion?.ApprovalDate, holding, payers, agreements,
      (p.UpdatedAt ?? p.CreatedAt)?.ToString("O"));
  }

  public async Task<ExternalRef?> TownAsync(Guid townId, CancellationToken cancellationToken) =>
      (await Get<MasterEnvelope>($"masters/towns/{townId}", cancellationToken))?.Item is { } t ? new ExternalRef(t.Id, t.Code, t.Name) : null;

  public async Task<ExternalRef?> PropertyTypeAsync(Guid propertyTypeId, CancellationToken cancellationToken) =>
      (await Get<MasterEnvelope>($"masters/property-types/{propertyTypeId}", cancellationToken))?.Item is { } t ? new ExternalRef(t.Id, t.Code, t.Name) : null;

  private Task<T?> Get<T>(string path, CancellationToken cancellationToken) where T : class => ServiceJson.GetAsync<T>(http, Service, path, logger, cancellationToken);

  private static string? Code(MasterRefDto? master) => master?.Code;

  // ---- what Taxation reads of Property's responses ----

  private sealed record MasterRefDto(Guid Id, string Code, string Name);
  private sealed record OwnerRefDto(Guid Id, string OwnerCode, string OwnerName);
  private sealed record AreaDto(Guid? CurrentMeasurementId, decimal? TotalAreaSqFt, decimal? BuiltUpAreaSqFt, decimal RegularizedAreaSqFt);
  private sealed record OwnershipRow(Guid Id, OwnerRefDto? Owner, decimal OwnershipSharePct, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string? Status);
  private sealed record PropertyDetail(Guid Id, string PropertyCode, string PropertyName, MasterRefDto? Town, MasterRefDto? PropertyType, MasterRefDto? Status,
    MasterRefDto? Classification, string? AddressLine, bool IsActive, AreaDto? Area, IReadOnlyList<OwnershipRow>? CurrentOwners, DateTime? CreatedAt, DateTime? UpdatedAt);
  private sealed record PropertyEnvelope(PropertyDetail Property);
  private sealed record MeasurementRow(Guid Id, MasterRefDto? Unit, decimal TotalArea, decimal BuiltUpArea, bool IsCurrent);
  private sealed record MeasurementsEnvelope(IReadOnlyList<MeasurementRow> Measurements);
  private sealed record PlanRow(Guid Id, string PlanNo, int RevisionNo, MasterRefDto? PlanType, MasterRefDto? Status, DateOnly? ApprovalDate, decimal? CoveredArea, MasterRefDto? Unit);
  private sealed record PlansEnvelope(IReadOnlyList<PlanRow> Plans);
  private sealed record LeaseRow(Guid Id, string LeaseNo, OwnerRefDto? Lessee, MasterRefDto? Status, DateOnly LeaseStartDate, DateOnly LeaseEndDate, int? LeaseTermYears,
    decimal? LeaseAmount, string? AmountFrequency, DateOnly? TerminationDate);
  private sealed record LeasesEnvelope(IReadOnlyList<LeaseRow> Leases);
  private sealed record RentalRow(Guid Id, string RentalNo, OwnerRefDto? Tenant, MasterRefDto? Status, DateOnly RentalStartDate, DateOnly? RentalEndDate, decimal RentAmount,
    string? RentFrequency, decimal? AnnualIncreasePct, DateOnly? TerminationDate);
  private sealed record RentalsEnvelope(IReadOnlyList<RentalRow> Rentals);
  private sealed record AllotmentRow(Guid Id, OwnerRefDto? Allottee, DateOnly AllotmentDate, bool IsInForce);
  private sealed record AllotmentsEnvelope(IReadOnlyList<AllotmentRow> Allotments);
  private sealed record ContactRow(string ContactNumber, bool IsPrimary, bool IsActive);
  private sealed record AddressRow(string? AddressType, string FullAddress, string? CityTown, string? District, bool IsPrimary, bool IsActive);
  private sealed record OwnerDetail(Guid Id, string OwnerCode, string OwnerName, string? FatherHusbandName, string? Cnic, string? Ntn,
    IReadOnlyList<ContactRow>? Contacts, IReadOnlyList<AddressRow>? Addresses);
  private sealed record OwnerEnvelope(OwnerDetail Owner);
  private sealed record MasterRow(Guid Id, string Code, string Name, decimal? FactorToBase);
  private sealed record MastersEnvelope(IReadOnlyList<MasterRow> Items);
  private sealed record MasterEnvelope(MasterRow Item);
}

/// Reads the holiday calendar and post holders from the HRM service. Both are open to any signed-in person: holidays
/// through /holidays, who holds a post through the staff directory (/directory/posts/{id}/holder), so a clerk without
/// HR access still gets the officer who signs.
public sealed class HrmServiceClient(HttpClient http, ILogger<HrmServiceClient> logger) : IHrmDirectory
{
  private const string Service = "HRM";

  public bool IsConfigured => http.BaseAddress is not null;

  public async Task<IReadOnlySet<DateOnly>> HolidaysAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
  {
    var result = await ServiceJson.GetAsync<HolidaysEnvelope>(http, Service, $"holidays?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", logger, cancellationToken);
    return (result?.Holidays ?? []).Select(h => h.HolidayDate).ToHashSet();
  }

  public async Task<PostHolderInfo?> PostHolderAsync(Guid postId, DateOnly asOf, CancellationToken cancellationToken)
  {
    var post = await ServiceJson.GetAsync<HolderEnvelope>(http, Service, $"directory/posts/{postId}/holder?on={asOf:yyyy-MM-dd}", logger, cancellationToken);
    if (post is null)
      return null;
    var h = post.Holder;
    return new PostHolderInfo(postId, post.PostCode, post.Designation, h?.Id, h?.EmployeeNumber, h?.Name, h?.Designation);
  }

  private sealed record HolidayRow(DateOnly HolidayDate);
  private sealed record HolidaysEnvelope(IReadOnlyList<HolidayRow> Holidays);
  private sealed record DirectoryEmployee(Guid Id, string? EmployeeNumber, string? Name, string? Designation);
  private sealed record HolderEnvelope(Guid PostId, string? PostCode, string? Designation, DirectoryEmployee? Holder);
}
