using Microsoft.AspNetCore.Http;

/// Fills in the actor (from the access token) and the request's IP and device, then adds the line
/// to the context. The caller's SaveChanges persists it alongside the change it describes.
public sealed class ActivityRecorder(
    IApplicationDbContext context,
    ICurrentUser currentUser,
    IHttpContextAccessor httpContextAccessor) : IActivityRecorder
{
  public async Task RecordAsync(
      ActivityAction action,
      ActivityTargetType targetType,
      Guid? targetId,
      string? targetLabel,
      string? detail,
      CancellationToken cancellationToken)
  {
    if (currentUser.UserId is not { } actorId)
      return;

    var http = httpContextAccessor.HttpContext;
    var ip = IpAddress.OfNullable(http?.Connection.RemoteIpAddress?.ToString());
    var userAgent = http?.Request.Headers.UserAgent.ToString();

    var activity = AdminActivity.Record(
      UserId.Of(actorId),
      currentUser.Username ?? currentUser.AuditName,
      action, targetType, targetId, targetLabel, Fit(detail),
      ip, userAgent, DateTime.UtcNow);

    await context.AdminActivities.AddAsync(activity, cancellationToken);
  }

  // the column holds 500 characters; a longer detail must never fail the change it describes
  private static string? Fit(string? detail) =>
    detail is { Length: > DetailMaxLength } ? detail[..(DetailMaxLength - 1)] + "\u2026" : detail;

  private const int DetailMaxLength = 500;
}
