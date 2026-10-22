using Microsoft.AspNetCore.Http;

/// Where the current request came from, as an audit trail writes it: the person's own address (the gateway's
/// X-Forwarded-For is honoured, see AddErpAuthentication), the device (browser user agent) and the request id that ties the
/// lines of one save together. All null for background work.
public interface IRequestOrigin
{
  string? IpAddress { get; }
  string? Device { get; }
  string? RequestId { get; }
}

public sealed class RequestOrigin(IHttpContextAccessor httpContextAccessor) : IRequestOrigin
{
  private HttpContext? Http => httpContextAccessor.HttpContext;

  public string? IpAddress
  {
    get
    {
      var address = Http?.Connection.RemoteIpAddress;
      if (address is null)
        return null;
      return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
    }
  }

  public string? Device
  {
    get
    {
      var agent = Http?.Request.Headers.UserAgent.ToString();
      return string.IsNullOrWhiteSpace(agent) ? null : agent.Length > 300 ? agent[..300] : agent;
    }
  }

  public string? RequestId => Http?.TraceIdentifier;
}
