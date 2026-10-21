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

  public string? HrmBaseUrl { get; set; }
  public int TimeoutSeconds { get; set; } = 20;
}

/// Passes the signed-in user's token on, so HRM applies that user's own rights (its directory answers anyone signed in).
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

/// HRM's staff directory (GET /directory/employees, /directory/org-units).
public sealed class HrmDirectoryClient(HttpClient http, ILogger<HrmDirectoryClient> logger) : IHrmDirectory
{
  private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

  private IReadOnlyList<OrgUnitRef>? _units;

  public bool IsConfigured => http.BaseAddress is not null;

  public async Task<StaffMember?> EmployeeAsync(Guid employeeId, CancellationToken cancellationToken)
  {
    var page = await GetAsync<EmployeesEnvelope>($"directory/employees?ids={employeeId}&pageSize=1", cancellationToken);
    var row = page?.Employees?.Data?.FirstOrDefault(e => e.Id == employeeId);
    return row is null ? null : new StaffMember(row.Id, row.EmployeeNumber, row.Name, row.Designation, row.OrgUnit, row.OrgUnitId, row.Status ?? "", row.InService);
  }

  public async Task<IReadOnlyList<OrgUnitRef>> OrgUnitsAsync(CancellationToken cancellationToken)
  {
    if (_units is not null)
      return _units;
    var result = await GetAsync<UnitsEnvelope>("directory/org-units", cancellationToken);
    _units = (result?.Units ?? []).Select(u => new OrgUnitRef(u.Id, u.Code, u.Name, u.ParentId)).ToList();
    return _units;
  }

  private async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken) where T : class
  {
    if (http.BaseAddress is null)
      throw new ExternalServiceException("HR is not linked to Assets on this server (Services:HrmBaseUrl is not set), so custodians and departments cannot be checked.");

    HttpResponseMessage response;
    try
    {
      response = await http.GetAsync(path, cancellationToken);
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
    {
      logger.LogWarning(ex, "HRM did not answer {Path}", path);
      throw new ExternalServiceException("The HR service is not reachable, so the custodian or department cannot be checked. Try again in a moment.", ex);
    }

    using (response)
    {
      if (response.StatusCode == HttpStatusCode.NotFound)
        return null;
      if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        throw new ExternalServiceException("The HR service refused to show its staff directory to the signed-in user.");
      if (!response.IsSuccessStatusCode)
      {
        logger.LogWarning("HRM answered {Status} to {Path}", (int)response.StatusCode, path);
        throw new ExternalServiceException("The HR service could not answer, so the custodian or department cannot be checked. Try again in a moment.");
      }
      return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
    }
  }

  private sealed record EmployeeRow(Guid Id, string EmployeeNumber, string Name, string? Designation, string? OrgUnit, Guid? OrgUnitId, string? Status, bool InService);
  private sealed record EmployeesPage(IReadOnlyList<EmployeeRow>? Data);
  private sealed record EmployeesEnvelope(EmployeesPage? Employees);
  private sealed record UnitRow(Guid Id, string Code, string Name, Guid? ParentId);
  private sealed record UnitsEnvelope(IReadOnlyList<UnitRow>? Units);
}
